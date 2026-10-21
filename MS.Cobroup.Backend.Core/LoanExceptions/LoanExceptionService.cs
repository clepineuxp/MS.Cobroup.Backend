using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Core.Routes;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Customers;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.LoanExceptions;

namespace MS.Cobroup.Backend.Core.LoanExceptions;

public sealed class LoanExceptionService(
    ILoanExceptionRepository repository,
    ICustomerRepository customerRepository,
    IAdministrativeRouteDirectory routeDirectory)
{
    public async Task<LoanExceptionResult> CreateAsync(
        CreateLoanExceptionCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Input.ProposedAmount <= 0)
        {
            return LoanExceptionResult.Invalid("Proposed amount must be positive.");
        }

        if (command.Input.ProposedDays <= 0)
        {
            return LoanExceptionResult.Invalid("Proposed days must be positive.");
        }

        if (command.Input.ProposedPercentage <= 0)
        {
            return LoanExceptionResult.Invalid("Proposed percentage must be positive.");
        }

        if (string.IsNullOrWhiteSpace(command.Input.Reason))
        {
            return LoanExceptionResult.Invalid("Reason is required.");
        }

        var routes = await routeDirectory.ListAccessibleAsync(command.AccessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapFailure(routes.Outcome);
        }

        var customer = await customerRepository.FindAsync(command.Actor.TenantId, command.Input.CustomerId, cancellationToken);
        if (customer is null || routes.Routes.All(r => r.Id != customer.RouteId))
        {
            return LoanExceptionResult.NotFound();
        }

        if (customer.Status != CustomerStatus.Active)
        {
            return LoanExceptionResult.Invalid("Cannot request an exception for an inactive customer.");
        }

        LoanExceptionRequest exception;
        try
        {
            exception = LoanExceptionRequest.Create(
                command.Actor.TenantId,
                customer.Id,
                customer.RouteId,
                command.Input.ProposedAmount,
                command.Input.ProposedDays,
                command.Input.ProposedPercentage,
                command.Input.Reason,
                command.Actor.UserId,
                command.Actor.Role,
                DateTimeOffset.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return LoanExceptionResult.Invalid(ex.Message);
        }

        await repository.AddAsync(exception, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        var customerName = $"{customer.FirstNames} {customer.LastNames}".Trim();
        return LoanExceptionResult.Created(ToDetails(exception, customerName));
    }

    public async Task<LoanExceptionResult> GetAsync(
        GetLoanExceptionQuery query,
        CancellationToken cancellationToken)
    {
        var routes = await routeDirectory.ListAccessibleAsync(query.AccessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapFailure(routes.Outcome);
        }

        var exception = await repository.FindAsync(query.Actor.TenantId, query.ExceptionId, cancellationToken);
        if (exception is null || routes.Routes.All(r => r.Id != exception.RouteId))
        {
            return LoanExceptionResult.NotFound();
        }

        var customer = await customerRepository.FindAsync(query.Actor.TenantId, exception.CustomerId, cancellationToken);
        var customerName = customer is not null ? $"{customer.FirstNames} {customer.LastNames}".Trim() : string.Empty;

        return LoanExceptionResult.Success(ToDetails(exception, customerName));
    }

    public async Task<LoanExceptionSearchResult> SearchAsync(
        SearchLoanExceptionsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100)
        {
            return LoanExceptionSearchResult.Invalid("Page must be positive and pageSize must be between 1 and 100.");
        }

        var routes = await routeDirectory.ListAccessibleAsync(query.AccessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapSearchFailure(routes.Outcome);
        }

        var accessibleRouteIds = routes.Routes.Select(r => r.Id).ToHashSet();
        if (query.RouteId.HasValue && !accessibleRouteIds.Contains(query.RouteId.Value))
        {
            return LoanExceptionSearchResult.Forbidden();
        }

        var page = await repository.SearchAsync(
            query.Actor.TenantId,
            accessibleRouteIds,
            query.RouteId,
            query.CustomerId,
            query.Status,
            query.Page,
            query.PageSize,
            cancellationToken);

        return LoanExceptionSearchResult.Success(page);
    }

    public async Task<LoanExceptionResult> CancelAsync(
        CancelLoanExceptionCommand command,
        CancellationToken cancellationToken)
    {
        var routes = await routeDirectory.ListAccessibleAsync(command.AccessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapFailure(routes.Outcome);
        }

        var exception = await repository.FindAsync(command.Actor.TenantId, command.ExceptionId, cancellationToken);
        if (exception is null || routes.Routes.All(r => r.Id != exception.RouteId))
        {
            return LoanExceptionResult.NotFound();
        }

        if (command.Actor.Role is not UserRole.Admin && command.Actor.UserId != exception.CreatedByUserId)
        {
            return LoanExceptionResult.Forbidden();
        }

        try
        {
            var modified = exception.Cancel(command.Actor.UserId, DateTimeOffset.UtcNow);
            if (modified)
            {
                await repository.SaveChangesAsync(cancellationToken);
            }
        }
        catch (InvalidOperationException ex)
        {
            return LoanExceptionResult.Conflict(ex.Message);
        }

        var customer = await customerRepository.FindAsync(command.Actor.TenantId, exception.CustomerId, cancellationToken);
        var customerName = customer is not null ? $"{customer.FirstNames} {customer.LastNames}".Trim() : string.Empty;

        return LoanExceptionResult.Success(ToDetails(exception, customerName));
    }

    public async Task<LoanExceptionResult> ApplyDecisionAsync(
        ApplyLoanExceptionDecisionCommand command,
        CancellationToken cancellationToken)
    {
        // AC-08: Only Admin can approve or reject loan exception requests
        if (command.Actor.Role is not UserRole.Admin)
        {
            return LoanExceptionResult.Forbidden();
        }

        var exception = await repository.FindAsync(command.Actor.TenantId, command.ExceptionId, cancellationToken);
        if (exception is null)
        {
            return LoanExceptionResult.NotFound();
        }

        try
        {
            var modified = exception.ApplyDecision(
                command.Input.Decision,
                command.Actor.UserId,
                command.Input.DecisionNotes,
                DateTimeOffset.UtcNow);

            if (modified)
            {
                await repository.SaveChangesAsync(cancellationToken);
            }
        }
        catch (InvalidOperationException ex)
        {
            return LoanExceptionResult.Conflict(ex.Message);
        }

        var customer = await customerRepository.FindAsync(command.Actor.TenantId, exception.CustomerId, cancellationToken);
        var customerName = customer is not null ? $"{customer.FirstNames} {customer.LastNames}".Trim() : string.Empty;

        return LoanExceptionResult.Success(ToDetails(exception, customerName));
    }

    private static LoanExceptionDetails ToDetails(LoanExceptionRequest exception, string customerName) =>
        new(
            exception.Id,
            exception.CustomerId,
            customerName,
            exception.RouteId,
            exception.ProposedAmount,
            exception.ProposedDays,
            exception.ProposedPercentage,
            exception.Reason,
            exception.Status,
            exception.CreatedByUserId,
            exception.CreatedByRole,
            exception.CreatedAtUtc,
            exception.DecidedByUserId,
            exception.DecidedAtUtc,
            exception.DecisionNotes,
            exception.CancelledByUserId,
            exception.CancelledAtUtc);

    private static LoanExceptionResult MapFailure(RouteDirectoryOutcome outcome) => outcome switch
    {
        RouteDirectoryOutcome.Unauthorized => LoanExceptionResult.Unauthorized(),
        RouteDirectoryOutcome.Forbidden => LoanExceptionResult.Forbidden(),
        _ => LoanExceptionResult.Unavailable()
    };

    private static LoanExceptionSearchResult MapSearchFailure(RouteDirectoryOutcome outcome) => outcome switch
    {
        RouteDirectoryOutcome.Unauthorized => LoanExceptionSearchResult.Unauthorized(),
        RouteDirectoryOutcome.Forbidden => LoanExceptionSearchResult.Forbidden(),
        _ => LoanExceptionSearchResult.Unavailable()
    };
}
