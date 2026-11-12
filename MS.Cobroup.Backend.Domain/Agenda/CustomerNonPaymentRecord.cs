namespace MS.Cobroup.Backend.Domain.Agenda;

public sealed class CustomerNonPaymentRecord
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid RouteId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? LoanId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public Guid? ReasonId { get; private set; }
    public string ReasonName { get; private set; } = string.Empty;
    public string? Observations { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }

    private CustomerNonPaymentRecord() { }

    public CustomerNonPaymentRecord(
        Guid id,
        Guid tenantId,
        Guid routeId,
        Guid customerId,
        Guid? loanId,
        DateOnly businessDate,
        Guid? reasonId,
        string reasonName,
        string? observations,
        Guid recordedByUserId,
        DateTimeOffset recordedAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        RouteId = routeId;
        CustomerId = customerId;
        LoanId = loanId;
        BusinessDate = businessDate;
        ReasonId = reasonId;
        ReasonName = reasonName.Trim();
        Observations = observations?.Trim();
        RecordedByUserId = recordedByUserId;
        RecordedAtUtc = recordedAtUtc;
    }
}

