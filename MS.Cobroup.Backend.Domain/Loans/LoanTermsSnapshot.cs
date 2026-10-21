namespace MS.Cobroup.Backend.Domain.Loans;

public sealed record LoanTermsSnapshot(
    decimal PrincipalAmount,
    decimal InterestRate,
    decimal InterestAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    PaymentFrequency PaymentFrequency,
    int? FrequencyNDays,
    int InstallmentCount,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset DueAtUtc)
{
    public static LoanTermsSnapshot Create(
        decimal principalAmount,
        decimal interestRate,
        decimal interestAmount,
        decimal totalAmount,
        PaymentFrequency paymentFrequency,
        int? frequencyNDays,
        int installmentCount,
        DateTimeOffset startsAtUtc,
        DateTimeOffset dueAtUtc)
    {
        if (principalAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(principalAmount), "Principal amount must be positive.");
        }

        if (interestRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(interestRate), "Interest rate must be positive.");
        }

        if (interestAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(interestAmount), "Interest amount cannot be negative.");
        }

        if (totalAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAmount), "Total amount must be positive.");
        }

        if (installmentCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(installmentCount), "Installment count must be positive.");
        }

        if (paymentFrequency is PaymentFrequency.EveryNDays && (!frequencyNDays.HasValue || frequencyNDays.Value <= 0))
        {
            throw new ArgumentException("Frequency N days must be positive when EveryNDays is selected.", nameof(frequencyNDays));
        }

        return new(
            principalAmount,
            interestRate,
            interestAmount,
            totalAmount,
            0m,
            totalAmount,
            paymentFrequency,
            frequencyNDays,
            installmentCount,
            startsAtUtc,
            dueAtUtc);
    }
}
