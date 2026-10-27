using MS.Cobroup.Backend.Core.Loans;
using MS.Cobroup.Backend.Domain.Loans;

namespace MS.Cobroup.Backend.Core.Tests.Loans;

public sealed class LoanScheduleCalculatorTests
{
    [Theory]
    [InlineData(2026, 1, 31, 2026, 2, 28, 2026, 3, 31)]
    [InlineData(2028, 1, 31, 2028, 2, 29, 2028, 3, 31)]
    [InlineData(2026, 12, 15, 2027, 1, 15, 2027, 2, 15)]
    [InlineData(2026, 1, 30, 2026, 2, 28, 2026, 3, 30)]
    public void MonthlyUsesCalendarMonthsAndPreservesOriginalDay(int year, int month, int day,
        int y1, int m1, int d1, int y2, int m2, int d2)
    {
        var schedule = LoanScheduleCalculator.Calculate(777.77m, 12.5m, PaymentFrequency.Monthly, 7, 3, new(year, month, day));
        Assert.Equal(new DateOnly(y1, m1, d1), schedule.Installments[0].DueDate);
        Assert.Equal(new DateOnly(y2, m2, d2), schedule.Installments[1].DueDate);
        Assert.Equal(new DateOnly(year, month, day).AddMonths(3), schedule.DueDate);
        Assert.Null(schedule.FrequencyNDays);
        Assert.Equal(schedule.TotalAmount, schedule.Installments.Sum(item => item.ExpectedAmount));
    }

    [Fact]
    public void RejectsUnknownFrequencyRatherThanTreatingItAsDaily()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoanScheduleCalculator.Calculate(100m, 10m, (PaymentFrequency)99, null, 2, new(2026, 1, 1)));
    }

    [Fact]
    public void Calculate_daily_schedule_computes_exact_sum_and_dates()
    {
        var principal = 1000m;
        var interestRate = 10m; // 10%
        var installmentCount = 5;
        var startDate = new DateOnly(2026, 11, 1);

        var schedule = LoanScheduleCalculator.Calculate(
            principal,
            interestRate,
            PaymentFrequency.Daily,
            null,
            installmentCount,
            startDate);

        Assert.Equal(principal, schedule.PrincipalAmount);
        Assert.Equal(interestRate, schedule.InterestRate);
        Assert.Equal(100m, schedule.InterestAmount);
        Assert.Equal(1100m, schedule.TotalAmount);
        Assert.Equal(5, schedule.Installments.Count);
        Assert.Equal(1100m, schedule.Installments.Sum(i => i.ExpectedAmount));
        Assert.Equal(new DateOnly(2026, 11, 6), schedule.DueDate);

        Assert.Equal(1, schedule.Installments[0].Number);
        Assert.Equal(new DateOnly(2026, 11, 2), schedule.Installments[0].DueDate);
        Assert.Equal(220m, schedule.Installments[0].ExpectedAmount);

        Assert.Equal(5, schedule.Installments[4].Number);
        Assert.Equal(new DateOnly(2026, 11, 6), schedule.Installments[4].DueDate);
        Assert.Equal(220m, schedule.Installments[4].ExpectedAmount);
    }

    [Fact]
    public void Calculate_zero_percent_interest_schedule_has_zero_interest_and_correct_installments()
    {
        var principal = 1000m;
        var interestRate = 0m;
        var installmentCount = 4;
        var startDate = new DateOnly(2026, 11, 1);

        var schedule = LoanScheduleCalculator.Calculate(
            principal,
            interestRate,
            PaymentFrequency.Daily,
            null,
            installmentCount,
            startDate);

        Assert.Equal(principal, schedule.PrincipalAmount);
        Assert.Equal(0m, schedule.InterestRate);
        Assert.Equal(0m, schedule.InterestAmount);
        Assert.Equal(1000m, schedule.TotalAmount);
        Assert.Equal(4, schedule.Installments.Count);
        Assert.Equal(250m, schedule.Installments[0].ExpectedAmount);
        Assert.Equal(250m, schedule.Installments[3].ExpectedAmount);
        Assert.Equal(1000m, schedule.Installments.Sum(i => i.ExpectedAmount));
    }

    [Fact]
    public void Calculate_weekly_schedule_advances_by_seven_days()
    {
        var principal = 500m;
        var interestRate = 20m;
        var installmentCount = 4;
        var startDate = new DateOnly(2026, 11, 2); // Monday

        var schedule = LoanScheduleCalculator.Calculate(
            principal,
            interestRate,
            PaymentFrequency.Weekly,
            null,
            installmentCount,
            startDate);

        Assert.Equal(600m, schedule.TotalAmount);
        Assert.Equal(4, schedule.Installments.Count);
        Assert.Equal(new DateOnly(2026, 11, 9), schedule.Installments[0].DueDate);
        Assert.Equal(new DateOnly(2026, 11, 16), schedule.Installments[1].DueDate);
        Assert.Equal(new DateOnly(2026, 11, 23), schedule.Installments[2].DueDate);
        Assert.Equal(new DateOnly(2026, 11, 30), schedule.Installments[3].DueDate);
        Assert.Equal(new DateOnly(2026, 11, 30), schedule.DueDate);
    }

    [Fact]
    public void Calculate_every_n_days_advances_by_specified_interval()
    {
        var principal = 300m;
        var interestRate = 15m;
        var installmentCount = 3;
        var startDate = new DateOnly(2026, 11, 1);

        var schedule = LoanScheduleCalculator.Calculate(
            principal,
            interestRate,
            PaymentFrequency.EveryNDays,
            3,
            installmentCount,
            startDate);

        Assert.Equal(3, schedule.Installments.Count);
        Assert.Equal(new DateOnly(2026, 11, 4), schedule.Installments[0].DueDate);
        Assert.Equal(new DateOnly(2026, 11, 7), schedule.Installments[1].DueDate);
        Assert.Equal(new DateOnly(2026, 11, 10), schedule.Installments[2].DueDate);
        Assert.Equal(new DateOnly(2026, 11, 10), schedule.DueDate);
    }

    [Theory]
    [InlineData(100, 5, 3)]       // 105 / 3 = 35.00
    [InlineData(100, 10, 3)]      // 110 / 3 = 36.67, 36.67, 36.66 (sum = 110.00)
    [InlineData(1000, 15, 6)]     // 1150 / 6 = 191.67, 191.67, 191.67, 191.67, 191.67, 191.65 (sum = 1150.00)
    [InlineData(777.77, 12.5, 7)] // Odd principal and rate
    [InlineData(10000, 20, 24)]   // Long installment count
    public void Calculate_guarantees_ac10_exact_cent_sum_equals_total_amount(
        decimal principal,
        decimal rate,
        int installments)
    {
        var schedule = LoanScheduleCalculator.Calculate(
            principal,
            rate,
            PaymentFrequency.Daily,
            null,
            installments,
            new DateOnly(2026, 11, 1));

        var sum = schedule.Installments.Sum(i => i.ExpectedAmount);
        Assert.Equal(schedule.TotalAmount, sum);
    }

    [Fact]
    public void Calculate_throws_on_invalid_arguments()
    {
        var startDate = new DateOnly(2026, 11, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoanScheduleCalculator.Calculate(-100m, 10m, PaymentFrequency.Daily, null, 5, startDate));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoanScheduleCalculator.Calculate(100m, -5m, PaymentFrequency.Daily, null, 5, startDate));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoanScheduleCalculator.Calculate(100m, 10m, PaymentFrequency.Daily, null, 0, startDate));

        Assert.Throws<ArgumentException>(() =>
            LoanScheduleCalculator.Calculate(100m, 10m, PaymentFrequency.EveryNDays, null, 5, startDate));

        Assert.Throws<ArgumentException>(() =>
            LoanScheduleCalculator.Calculate(100m, 10m, PaymentFrequency.EveryNDays, 0, 5, startDate));
    }
}
