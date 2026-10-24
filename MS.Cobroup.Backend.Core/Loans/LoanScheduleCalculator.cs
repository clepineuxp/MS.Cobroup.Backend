using MS.Cobroup.Backend.Domain.Loans;

namespace MS.Cobroup.Backend.Core.Loans;

public static class LoanScheduleCalculator
{
    public static CalculatedSchedule Calculate(
        decimal principalAmount,
        decimal interestRate,
        PaymentFrequency frequency,
        int? frequencyNDays,
        int installmentCount,
        DateOnly startDate)
    {
        if (principalAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(principalAmount), "Principal amount must be positive.");
        }

        if (interestRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(interestRate), "Interest rate must be positive.");
        }

        if (installmentCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(installmentCount), "Installment count must be positive.");
        }

        if (frequency is PaymentFrequency.EveryNDays && (!frequencyNDays.HasValue || frequencyNDays.Value <= 0))
        {
            throw new ArgumentException("Frequency N days must be positive when EveryNDays is specified.", nameof(frequencyNDays));
        }

        if (!Enum.IsDefined(frequency))
            throw new ArgumentOutOfRangeException(nameof(frequency), "La frecuencia de pago es inválida.");
        if (frequency != PaymentFrequency.EveryNDays) frequencyNDays = null;

        // Interest calculation: Simple interest on principal rounded to 2 decimals
        var interestAmount = Math.Round(principalAmount * (interestRate / 100m), 2, MidpointRounding.AwayFromZero);
        var totalAmount = principalAmount + interestAmount;

        // Base installment amount rounded to 2 decimals
        var baseInstallment = Math.Round(totalAmount / installmentCount, 2, MidpointRounding.AwayFromZero);

        var installments = new List<CalculatedInstallment>(installmentCount);
        var runningSum = 0m;

        var stepDays = frequency switch
        {
            PaymentFrequency.Daily => 1,
            PaymentFrequency.Weekly => 7,
            PaymentFrequency.EveryNDays => frequencyNDays!.Value,
            PaymentFrequency.Monthly => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(frequency))
        };

        var currentDate = startDate;

        for (var i = 1; i <= installmentCount; i++)
        {
            // Anchor every month to the original day, so February does not shift March's due date.
            currentDate = frequency is PaymentFrequency.Monthly
                ? startDate.AddMonths(i)
                : currentDate.AddDays(stepDays);

            decimal amount;
            if (i == installmentCount)
            {
                // AC-10: Adjust last installment with rounding residue so sum matches totalAmount exactly
                amount = totalAmount - runningSum;
            }
            else
            {
                amount = baseInstallment;
                runningSum += amount;
            }

            installments.Add(new CalculatedInstallment(i, currentDate, amount));
        }

        var dueDate = installments[^1].DueDate;

        return new CalculatedSchedule(
            principalAmount,
            interestRate,
            interestAmount,
            totalAmount,
            frequency,
            frequencyNDays,
            installmentCount,
            startDate,
            dueDate,
            installments);
    }
}

public sealed record CalculatedSchedule(
    decimal PrincipalAmount,
    decimal InterestRate,
    decimal InterestAmount,
    decimal TotalAmount,
    PaymentFrequency PaymentFrequency,
    int? FrequencyNDays,
    int InstallmentCount,
    DateOnly StartDate,
    DateOnly DueDate,
    IReadOnlyList<CalculatedInstallment> Installments);

public sealed record CalculatedInstallment(
    int Number,
    DateOnly DueDate,
    decimal ExpectedAmount);
