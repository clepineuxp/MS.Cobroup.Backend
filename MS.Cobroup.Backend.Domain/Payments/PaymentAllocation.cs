namespace MS.Cobroup.Backend.Domain.Payments;

public sealed class PaymentAllocation
{
    private PaymentAllocation()
    {
    }

    public PaymentAllocation(
        Guid id,
        Guid paymentId,
        Guid installmentId,
        int installmentNumber,
        decimal amountAllocated,
        bool isFullyCovered)
    {
        Id = id;
        PaymentId = paymentId;
        InstallmentId = installmentId;
        InstallmentNumber = installmentNumber;
        AmountAllocated = amountAllocated;
        IsFullyCovered = isFullyCovered;
    }

    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid InstallmentId { get; private set; }
    public int InstallmentNumber { get; private set; }
    public decimal AmountAllocated { get; private set; }
    public bool IsFullyCovered { get; private set; }

    public static PaymentAllocation Create(
        Guid paymentId,
        Guid installmentId,
        int installmentNumber,
        decimal amountAllocated,
        bool isFullyCovered)
    {
        if (amountAllocated <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountAllocated), "Allocated amount must be positive.");
        }

        return new(Guid.NewGuid(), paymentId, installmentId, installmentNumber, amountAllocated, isFullyCovered);
    }
}
