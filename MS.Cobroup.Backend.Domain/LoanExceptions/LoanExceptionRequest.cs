using MS.Cobroup.Backend.Domain.Authorization;

namespace MS.Cobroup.Backend.Domain.LoanExceptions;

public sealed class LoanExceptionRequest
{
    private LoanExceptionRequest()
    {
    }

    private LoanExceptionRequest(
        Guid tenantId,
        Guid customerId,
        Guid routeId,
        decimal proposedAmount,
        int proposedDays,
        decimal proposedPercentage,
        string reason,
        Guid createdByUserId,
        UserRole createdByRole,
        DateTimeOffset createdAtUtc)
    {
        if (tenantId == Guid.Empty || customerId == Guid.Empty || routeId == Guid.Empty || createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Tenant, customer, route and creator are required.");
        }

        if (proposedAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(proposedAmount), "Proposed amount must be positive.");
        }

        if (proposedDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(proposedDays), "Proposed days must be positive.");
        }

        if (proposedPercentage <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(proposedPercentage), "Proposed percentage must be positive.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason is required.", nameof(reason));
        }

        var normalizedReason = reason.Trim();
        if (normalizedReason.Length > 500)
        {
            throw new ArgumentException("Reason cannot exceed 500 characters.", nameof(reason));
        }

        Id = Guid.NewGuid();
        TenantId = tenantId;
        CustomerId = customerId;
        RouteId = routeId;
        ProposedAmount = decimal.Round(proposedAmount, 2);
        ProposedDays = proposedDays;
        ProposedPercentage = decimal.Round(proposedPercentage, 2);
        Reason = normalizedReason;
        Status = LoanExceptionStatus.Pending;
        CreatedByUserId = createdByUserId;
        CreatedByRole = createdByRole;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid RouteId { get; private set; }

    public decimal ProposedAmount { get; private set; }

    public int ProposedDays { get; private set; }

    public decimal ProposedPercentage { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public LoanExceptionStatus Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public UserRole CreatedByRole { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public string? DecisionNotes { get; private set; }

    public Guid? CancelledByUserId { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public static LoanExceptionRequest Create(
        Guid tenantId,
        Guid customerId,
        Guid routeId,
        decimal proposedAmount,
        int proposedDays,
        decimal proposedPercentage,
        string reason,
        Guid createdByUserId,
        UserRole createdByRole,
        DateTimeOffset createdAtUtc) =>
        new(
            tenantId,
            customerId,
            routeId,
            proposedAmount,
            proposedDays,
            proposedPercentage,
            reason,
            createdByUserId,
            createdByRole,
            createdAtUtc);

    public bool ApplyDecision(
        LoanExceptionDecision decision,
        Guid decidedByUserId,
        string? decisionNotes,
        DateTimeOffset decidedAtUtc)
    {
        if (decidedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Decider user id is required.", nameof(decidedByUserId));
        }

        var targetStatus = decision is LoanExceptionDecision.Approved
            ? LoanExceptionStatus.Approved
            : LoanExceptionStatus.Rejected;

        // Idempotency: if already decided to the same target status
        if (Status == targetStatus && DecidedByUserId == decidedByUserId)
        {
            return false;
        }

        if (Status != LoanExceptionStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot decide request with status '{Status}'. Only Pending requests can be decided.");
        }

        Status = targetStatus;
        DecidedByUserId = decidedByUserId;
        DecidedAtUtc = decidedAtUtc;
        DecisionNotes = string.IsNullOrWhiteSpace(decisionNotes) ? null : decisionNotes.Trim();
        return true;
    }

    public bool Cancel(Guid cancelledByUserId, DateTimeOffset cancelledAtUtc)
    {
        if (cancelledByUserId == Guid.Empty)
        {
            throw new ArgumentException("Canceller user id is required.", nameof(cancelledByUserId));
        }

        if (Status == LoanExceptionStatus.Cancelled && CancelledByUserId == cancelledByUserId)
        {
            return false;
        }

        if (Status != LoanExceptionStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot cancel request with status '{Status}'. Only Pending requests can be cancelled.");
        }

        Status = LoanExceptionStatus.Cancelled;
        CancelledByUserId = cancelledByUserId;
        CancelledAtUtc = cancelledAtUtc;
        return true;
    }
}
