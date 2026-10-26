namespace MS.Cobroup.Backend.Domain.Payments;

public sealed class Payment
{
    private readonly List<PaymentAllocation> allocations = [];
    private readonly List<PaymentEvidence> evidences = [];

    private Payment()
    {
    }

    private Payment(
        Guid id,
        Guid tenantId,
        Guid loanId,
        Guid customerId,
        Guid routeId,
        Guid collectorUserId,
        decimal amount,
        Guid paymentMethodId,
        string paymentMethodName,
        string? notes,
        string idempotencyKey,
        DateTimeOffset registeredAtUtc,
        IEnumerable<PaymentAllocation> initialAllocations)
    {
        Id = id;
        TenantId = tenantId;
        LoanId = loanId;
        CustomerId = customerId;
        RouteId = routeId;
        CollectorUserId = collectorUserId;
        Amount = amount;
        PaymentMethodId = paymentMethodId;
        PaymentMethodName = paymentMethodName;
        Notes = notes;
        IdempotencyKey = idempotencyKey;
        RegisteredAtUtc = registeredAtUtc;
        allocations.AddRange(initialAllocations);
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LoanId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid RouteId { get; private set; }
    public Guid CollectorUserId { get; private set; }
    public decimal Amount { get; private set; }
    public Guid PaymentMethodId { get; private set; }
    public string PaymentMethodName { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTimeOffset RegisteredAtUtc { get; private set; }

    public IReadOnlyCollection<PaymentAllocation> Allocations => allocations.AsReadOnly();
    public IReadOnlyCollection<PaymentEvidence> Evidences => evidences.AsReadOnly();

    public static Payment Register(
        Guid tenantId,
        Guid loanId,
        Guid customerId,
        Guid routeId,
        Guid collectorUserId,
        decimal amount,
        Guid paymentMethodId,
        string paymentMethodName,
        string? notes,
        string idempotencyKey,
        DateTimeOffset registeredAtUtc,
        IReadOnlyList<PaymentAllocation> allocations)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        }

        if (loanId == Guid.Empty)
        {
            throw new ArgumentException("Loan ID cannot be empty.", nameof(loanId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID cannot be empty.", nameof(customerId));
        }

        if (routeId == Guid.Empty)
        {
            throw new ArgumentException("Route ID cannot be empty.", nameof(routeId));
        }

        if (collectorUserId == Guid.Empty)
        {
            throw new ArgumentException("Collector user ID cannot be empty.", nameof(collectorUserId));
        }

        if (paymentMethodId == Guid.Empty)
        {
            throw new ArgumentException("Payment method ID cannot be empty.", nameof(paymentMethodId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(paymentMethodName);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be positive.");
        }

        if (allocations is null || allocations.Count == 0)
        {
            throw new ArgumentException("Allocations must not be empty.", nameof(allocations));
        }

        var sumAllocated = allocations.Sum(a => a.AmountAllocated);
        if (sumAllocated != amount)
        {
            throw new InvalidOperationException($"Sum of allocations ({sumAllocated}) must equal payment amount ({amount}) exactly.");
        }

        return new(
            Guid.NewGuid(),
            tenantId,
            loanId,
            customerId,
            routeId,
            collectorUserId,
            amount,
            paymentMethodId,
            paymentMethodName.Trim(),
            notes?.Trim(),
            idempotencyKey.Trim(),
            registeredAtUtc,
            allocations);
    }

    public void AddEvidence(PaymentEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        evidences.Add(evidence);
    }

    public void Update(
        decimal newAmount,
        Guid newPaymentMethodId,
        string newPaymentMethodName,
        string? newNotes,
        IEnumerable<PaymentAllocation> newAllocations)
    {
        if (newAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newAmount), "Payment amount must be positive.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(newPaymentMethodName);

        var allocList = newAllocations.ToList();
        var sumAllocated = allocList.Sum(a => a.AmountAllocated);
        if (sumAllocated != newAmount)
        {
            throw new InvalidOperationException($"Sum of allocations ({sumAllocated}) must equal payment amount ({newAmount}) exactly.");
        }

        Amount = newAmount;
        PaymentMethodId = newPaymentMethodId;
        PaymentMethodName = newPaymentMethodName.Trim();
        Notes = newNotes?.Trim();
        allocations.Clear();
        allocations.AddRange(allocList);
    }
}
