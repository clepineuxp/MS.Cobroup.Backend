using MS.Cobroup.Backend.Domain.Authorization;

namespace MS.Cobroup.Backend.Domain.Loans;

public sealed class Loan
{
    private readonly List<Installment> installments = [];

    private Loan()
    {
    }

    private Loan(
        Guid id,
        Guid tenantId,
        Guid customerId,
        Guid routeId,
        Guid? loanTemplateId,
        int? loanTemplateVersion,
        Guid? exceptionRequestId,
        LoanTermsSnapshot terms,
        LoanStatus status,
        Guid createdByUserId,
        UserRole createdByRole,
        DateTimeOffset createdAtUtc,
        IEnumerable<Installment> initialInstallments)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        RouteId = routeId;
        LoanTemplateId = loanTemplateId;
        LoanTemplateVersion = loanTemplateVersion;
        ExceptionRequestId = exceptionRequestId;
        Terms = terms;
        Status = status;
        CreatedByUserId = createdByUserId;
        CreatedByRole = createdByRole;
        CreatedAtUtc = createdAtUtc;
        installments.AddRange(initialInstallments);
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid RouteId { get; private set; }
    public Guid? LoanTemplateId { get; private set; }
    public int? LoanTemplateVersion { get; private set; }
    public Guid? ExceptionRequestId { get; private set; }
    public LoanTermsSnapshot Terms { get; private set; } = null!;
    public LoanStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public UserRole CreatedByRole { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<Installment> Installments => installments.AsReadOnly();

    public static Loan Originate(
        Guid tenantId,
        Guid customerId,
        Guid routeId,
        Guid? loanTemplateId,
        int? loanTemplateVersion,
        Guid? exceptionRequestId,
        LoanTermsSnapshot terms,
        Guid createdByUserId,
        UserRole createdByRole,
        DateTimeOffset createdAtUtc,
        IReadOnlyList<Installment> installments)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID cannot be empty.", nameof(customerId));
        }

        if (routeId == Guid.Empty)
        {
            throw new ArgumentException("Route ID cannot be empty.", nameof(routeId));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Created by user ID cannot be empty.", nameof(createdByUserId));
        }

        if (terms is null)
        {
            throw new ArgumentNullException(nameof(terms));
        }

        if (installments is null || installments.Count == 0)
        {
            throw new ArgumentException("Installments collection cannot be empty.", nameof(installments));
        }

        if (installments.Count != terms.InstallmentCount)
        {
            throw new ArgumentException($"Installments count ({installments.Count}) does not match terms count ({terms.InstallmentCount}).", nameof(installments));
        }

        // AC-10: Sum of installments must match total amount exactly
        var installmentsSum = installments.Sum(i => i.ExpectedAmount);
        if (installmentsSum != terms.TotalAmount)
        {
            throw new InvalidOperationException($"Sum of installments ({installmentsSum}) must match total amount ({terms.TotalAmount}) exactly.");
        }

        var loanId = Guid.NewGuid();

        return new(
            loanId,
            tenantId,
            customerId,
            routeId,
            loanTemplateId,
            loanTemplateVersion,
            exceptionRequestId,
            terms,
            LoanStatus.Active,
            createdByUserId,
            createdByRole,
            createdAtUtc,
            installments);
    }

    public void ApplyPayment(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be positive.");
        }

        if (Status != LoanStatus.Active)
        {
            throw new InvalidOperationException($"Cannot apply payment to loan {Id} because it is in status {Status}.");
        }

        if (amount > Terms.BalanceAmount)
        {
            throw new InvalidOperationException($"Payment amount ({amount}) exceeds current loan balance ({Terms.BalanceAmount}).");
        }

        var newPaid = Terms.PaidAmount + amount;
        var newBalance = Terms.BalanceAmount - amount;

        Terms = Terms with
        {
            PaidAmount = newPaid,
            BalanceAmount = newBalance
        };

        if (newBalance == 0m)
        {
            Status = LoanStatus.PaidOff;
        }
    }
}

