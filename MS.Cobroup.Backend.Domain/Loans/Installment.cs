namespace MS.Cobroup.Backend.Domain.Loans;

public sealed class Installment
{
    private Installment()
    {
    }

    public Installment(
        Guid id,
        Guid tenantId,
        Guid loanId,
        int number,
        DateOnly dueDate,
        decimal expectedAmount,
        decimal paidAmount,
        InstallmentStatus status)
    {
        Id = id;
        TenantId = tenantId;
        LoanId = loanId;
        Number = number;
        DueDate = dueDate;
        ExpectedAmount = expectedAmount;
        PaidAmount = paidAmount;
        Status = status;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LoanId { get; private set; }
    public int Number { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal ExpectedAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public InstallmentStatus Status { get; private set; }

    public static Installment Create(
        Guid tenantId,
        Guid loanId,
        int number,
        DateOnly dueDate,
        decimal expectedAmount)
    {
        if (expectedAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedAmount), "Expected amount must be positive.");
        }

        if (number <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "Installment number must be positive.");
        }

        return new(Guid.NewGuid(), tenantId, loanId, number, dueDate, expectedAmount, 0m, InstallmentStatus.Pending);
    }

    public void ApplyPayment(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be positive.");
        }

        var remaining = ExpectedAmount - PaidAmount;
        if (amount > remaining)
        {
            throw new InvalidOperationException($"Cannot apply {amount} to installment #{Number} with remaining balance {remaining}.");
        }

        PaidAmount += amount;
        Status = PaidAmount >= ExpectedAmount ? InstallmentStatus.Paid : InstallmentStatus.PartiallyPaid;
    }
}

