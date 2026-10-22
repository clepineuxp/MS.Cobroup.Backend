using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Core.LoanTemplates;
using MS.Cobroup.Backend.Core.Routes;
using MS.Cobroup.Backend.Domain.Customers;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.LoanExceptions;
using MS.Cobroup.Backend.Domain.Loans;

namespace MS.Cobroup.Backend.Core.Loans;

public sealed class LoanService(
    ILoanRepository loanRepository,
    ICustomerRepository customerRepository,
    ILoanExceptionRepository exceptionRepository,
    IAdministrativeRouteDirectory routeDirectory,
    IAdministrativeLoanTemplateDirectory templateDirectory)
{
    public async Task<LoanResult> OriginateAsync(
        OriginateLoanCommand command,
        CancellationToken cancellationToken)
    {
        var input = command.Input;

        if (input.PrincipalAmount <= 0)
        {
            return LoanResult.Invalid("El monto principal debe ser positivo.");
        }

        if (input.InterestRate <= 0)
        {
            return LoanResult.Invalid("La tasa de porcentaje debe ser positiva.");
        }

        if (input.InstallmentCount <= 0)
        {
            return LoanResult.Invalid("El número de cuotas debe ser positivo.");
        }

        if (input.PaymentFrequency is PaymentFrequency.EveryNDays && (!input.FrequencyNDays.HasValue || input.FrequencyNDays.Value <= 0))
        {
            return LoanResult.Invalid("Los días de frecuencia deben ser positivos cuando se selecciona Cada N Días.");
        }

        var routes = await routeDirectory.ListAccessibleAsync(command.AccessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapFailure(routes.Outcome);
        }

        var customer = await customerRepository.FindAsync(command.Actor.TenantId, input.CustomerId, cancellationToken);
        if (customer is null)
        {
            return LoanResult.NotFound("Cliente no encontrado.");
        }

        var accessibleRoute = routes.Routes.FirstOrDefault(r => r.Id == customer.RouteId);
        if (accessibleRoute is null)
        {
            // AC-01, AC-06: Customer must belong to an accessible route in actor's scope
            return LoanResult.Forbidden("El cliente no pertenece a una ruta en su alcance de operación.");
        }

        if (customer.Status != CustomerStatus.Active)
        {
            return LoanResult.Invalid("No se puede originar un préstamo para un cliente inactivo.");
        }

        // Calculate schedule early to check duration
        var schedule = LoanScheduleCalculator.Calculate(
            input.PrincipalAmount,
            input.InterestRate,
            input.PaymentFrequency,
            input.FrequencyNDays,
            input.InstallmentCount,
            input.StartDate);

        var scheduleDurationDays = schedule.DueDate.DayNumber - input.StartDate.DayNumber;

        Guid? templateId = null;
        int? templateVersion = null;
        Guid? exceptionRequestId = null;

        if (input.ExceptionRequestId.HasValue)
        {
            var exception = await exceptionRepository.FindAsync(command.Actor.TenantId, input.ExceptionRequestId.Value, cancellationToken);
            if (exception is null)
            {
                return LoanResult.NotFound("La solicitud de excepción indicada no existe.");
            }

            if (exception.CustomerId != customer.Id)
            {
                return LoanResult.Invalid("La solicitud de excepción no corresponde a este cliente.");
            }

            if (exception.RouteId != customer.RouteId)
            {
                return LoanResult.Invalid("La solicitud de excepción no corresponde a la ruta del cliente.");
            }

            // AC-08: Exception must be approved by Admin
            if (exception.Status != LoanExceptionStatus.Approved)
            {
                return LoanResult.Invalid("La solicitud de excepción no ha sido aprobada.");
            }

            if (exception.IsConsumed)
            {
                return LoanResult.Invalid("La solicitud de excepción ya ha sido consumida por otro préstamo.");
            }

            if (input.PrincipalAmount != exception.ProposedAmount)
            {
                return LoanResult.Invalid($"El monto solicitado ({input.PrincipalAmount:N2}) debe coincidir exactamente con el monto aprobado en la excepción ({exception.ProposedAmount:N2}).");
            }

            if (input.InterestRate != exception.ProposedPercentage)
            {
                return LoanResult.Invalid($"La tasa ({input.InterestRate:N2}%) debe coincidir con la tasa aprobada en la excepción ({exception.ProposedPercentage:N2}%).");
            }

            if (scheduleDurationDays > exception.ProposedDays)
            {
                return LoanResult.Invalid($"La duración del cronograma ({scheduleDurationDays} días calendario) supera el plazo aprobado en la excepción ({exception.ProposedDays} días).");
            }

            exceptionRequestId = exception.Id;
        }
        else if (input.LoanTemplateId.HasValue)
        {
            // AC-07: Template-based loan
            var templatesResult = await templateDirectory.ListAvailableAsync(command.AccessToken, cancellationToken);
            if (templatesResult.Outcome is not LoanTemplateDirectoryOutcome.Success)
            {
                return MapTemplateFailure(templatesResult.Outcome);
            }

            var template = templatesResult.Templates.FirstOrDefault(t => t.Id == input.LoanTemplateId.Value);
            if (template is null)
            {
                return LoanResult.NotFound("La plantilla de crédito indicada no está disponible o no existe.");
            }

            if (input.PrincipalAmount > template.MaxPrincipalAmount)
            {
                return LoanResult.Invalid($"El monto solicitado ({input.PrincipalAmount:N2}) supera el máximo permitido por la plantilla ({template.MaxPrincipalAmount:N2}). Requiere solicitar una excepción.");
            }

            // I-08: MaxLoanDays compared to schedule duration in calendar days, not installment count
            if (scheduleDurationDays > template.MaxLoanDays)
            {
                return LoanResult.Invalid($"La duración del cronograma ({scheduleDurationDays} días calendario) supera el plazo máximo de la plantilla ({template.MaxLoanDays} días). Requiere solicitar una excepción.");
            }

            if (!template.AllowedPercentages.Contains(input.InterestRate))
            {
                return LoanResult.Invalid($"La tasa ({input.InterestRate:N2}%) no está dentro de los porcentajes autorizados en la plantilla.");
            }

            templateId = template.Id;
            templateVersion = template.Version;
        }
        else
        {
            return LoanResult.Invalid("Debe seleccionar una plantilla publicada o una solicitud de excepción aprobada.");
        }

        var now = DateTimeOffset.UtcNow;
        var startsAtUtc = new DateTimeOffset(input.StartDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dueAtUtc = new DateTimeOffset(schedule.DueDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        // AC-09: Snapshot of terms
        var termsSnapshot = LoanTermsSnapshot.Create(
            schedule.PrincipalAmount,
            schedule.InterestRate,
            schedule.InterestAmount,
            schedule.TotalAmount,
            schedule.PaymentFrequency,
            schedule.FrequencyNDays,
            schedule.InstallmentCount,
            startsAtUtc,
            dueAtUtc);

        var loanId = Guid.NewGuid();
        var installments = schedule.Installments.Select(i =>
            Installment.Create(
                command.Actor.TenantId,
                loanId,
                i.Number,
                i.DueDate,
                i.ExpectedAmount)).ToList();

        var loan = Loan.Originate(
            command.Actor.TenantId,
            customer.Id,
            customer.RouteId,
            templateId,
            templateVersion,
            exceptionRequestId,
            termsSnapshot,
            command.Actor.UserId,
            command.Actor.Role,
            now,
            installments);

        // Atomic Check-and-Insert with route quota and exception consumption
        var outcome = await loanRepository.TryAddAsync(
            loan,
            accessibleRoute.MaxActiveLoans,
            exceptionRequestId,
            cancellationToken);

        if (outcome is LoanSaveOutcome.CapacityReached)
        {
            return LoanResult.Conflict("El cupo de préstamos activos para esta ruta ha sido alcanzado.");
        }

        if (outcome is LoanSaveOutcome.ExceptionAlreadyConsumed)
        {
            return LoanResult.Invalid("La solicitud de excepción ya ha sido consumida por otro préstamo.");
        }

        if (outcome is LoanSaveOutcome.Conflict)
        {
            return LoanResult.Conflict("Existe un conflicto de concurrencia al originar el préstamo.");
        }

        var customerName = $"{customer.FirstNames} {customer.LastNames}".Trim();
        return LoanResult.Created(ToDetails(loan, customerName, accessibleRoute.Name));
    }

    public async Task<LoanResult> GetAsync(
        GetLoanQuery query,
        CancellationToken cancellationToken)
    {
        var routes = await routeDirectory.ListAccessibleAsync(query.AccessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapFailure(routes.Outcome);
        }

        var loan = await loanRepository.FindAsync(query.Actor.TenantId, query.LoanId, cancellationToken);
        if (loan is null || routes.Routes.All(r => r.Id != loan.RouteId))
        {
            return LoanResult.NotFound();
        }

        var customer = await customerRepository.FindAsync(query.Actor.TenantId, loan.CustomerId, cancellationToken);
        var customerName = customer is not null ? $"{customer.FirstNames} {customer.LastNames}".Trim() : string.Empty;
        var routeName = routes.Routes.FirstOrDefault(r => r.Id == loan.RouteId)?.Name ?? string.Empty;

        return LoanResult.Success(ToDetails(loan, customerName, routeName));
    }

    public async Task<LoanSearchResult> SearchAsync(
        SearchLoansQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100)
        {
            return LoanSearchResult.Invalid("Page must be positive and pageSize must be between 1 and 100.");
        }

        var routes = await routeDirectory.ListAccessibleAsync(query.AccessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapSearchFailure(routes.Outcome);
        }

        var accessibleRouteIds = routes.Routes.Select(r => r.Id).ToHashSet();
        if (query.RouteId.HasValue && !accessibleRouteIds.Contains(query.RouteId.Value))
        {
            return LoanSearchResult.Forbidden();
        }

        var page = await loanRepository.SearchAsync(
            query.Actor.TenantId,
            accessibleRouteIds,
            query.RouteId,
            query.CustomerId,
            query.Status,
            query.Page,
            query.PageSize,
            cancellationToken);

        return LoanSearchResult.Success(page);
    }

    public async Task<LoanListResult> ListByCustomerAsync(
        CustomerActor actor,
        string accessToken,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var routes = await routeDirectory.ListAccessibleAsync(accessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapListFailure(routes.Outcome);
        }

        var customer = await customerRepository.FindAsync(actor.TenantId, customerId, cancellationToken);
        if (customer is null)
        {
            return LoanListResult.NotFound("Cliente no encontrado.");
        }

        if (routes.Routes.All(r => r.Id != customer.RouteId))
        {
            return LoanListResult.Forbidden();
        }

        var routeName = routes.Routes.FirstOrDefault(r => r.Id == customer.RouteId)?.Name ?? string.Empty;
        var customerName = $"{customer.FirstNames} {customer.LastNames}".Trim();

        var loans = await loanRepository.ListByCustomerAsync(actor.TenantId, customerId, cancellationToken);
        var details = loans.Select(l => ToDetails(l, customerName, routeName)).ToArray();

        return LoanListResult.Success(details);
    }

    private static LoanDetails ToDetails(Loan loan, string customerName, string routeName) =>
        new(
            loan.Id,
            loan.CustomerId,
            customerName,
            loan.RouteId,
            routeName,
            loan.LoanTemplateId,
            loan.LoanTemplateVersion,
            loan.ExceptionRequestId,
            loan.Status,
            loan.Terms.PrincipalAmount,
            loan.Terms.InterestRate,
            loan.Terms.InterestAmount,
            loan.Terms.TotalAmount,
            loan.Terms.PaidAmount,
            loan.Terms.BalanceAmount,
            loan.Terms.PaymentFrequency,
            loan.Terms.FrequencyNDays,
            loan.Terms.InstallmentCount,
            loan.Terms.StartsAtUtc,
            loan.Terms.DueAtUtc,
            loan.CreatedByUserId,
            loan.CreatedByRole,
            loan.CreatedAtUtc,
            loan.Installments.Select(i => new InstallmentDetails(
                i.Id,
                i.Number,
                i.DueDate,
                i.ExpectedAmount,
                i.PaidAmount,
                i.Status)).OrderBy(i => i.Number).ToArray());

    private static LoanResult MapFailure(RouteDirectoryOutcome outcome) => outcome switch
    {
        RouteDirectoryOutcome.Unauthorized => LoanResult.Unauthorized(),
        RouteDirectoryOutcome.Forbidden => LoanResult.Forbidden(),
        _ => LoanResult.Unavailable()
    };

    private static LoanSearchResult MapSearchFailure(RouteDirectoryOutcome outcome) => outcome switch
    {
        RouteDirectoryOutcome.Unauthorized => LoanSearchResult.Unauthorized(),
        RouteDirectoryOutcome.Forbidden => LoanSearchResult.Forbidden(),
        _ => LoanSearchResult.Unavailable()
    };

    private static LoanResult MapTemplateFailure(LoanTemplateDirectoryOutcome outcome) => outcome switch
    {
        LoanTemplateDirectoryOutcome.Unauthorized => LoanResult.Unauthorized(),
        LoanTemplateDirectoryOutcome.Forbidden => LoanResult.Forbidden(),
        _ => LoanResult.Unavailable()
    };

    private static LoanListResult MapListFailure(RouteDirectoryOutcome outcome) => outcome switch
    {
        RouteDirectoryOutcome.Unauthorized => LoanListResult.Unauthorized(),
        RouteDirectoryOutcome.Forbidden => LoanListResult.Forbidden(),
        _ => LoanListResult.Unavailable()
    };
}
