using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.Backend.Api.Controllers;
using MS.Cobroup.Backend.Core.Loans;
using MS.Cobroup.Backend.Domain.Loans;

namespace MS.Cobroup.Backend.Infrastructure.Tests;

public sealed class LoansControllerTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(3)]
    public void PreviewSchedule_zero_interest_returns_only_principal(int count)
    {
        var controller = new LoansController(null!);
        var result = controller.PreviewSchedule(new PreviewLoanScheduleRequest(
            1000m, 0m, PaymentFrequency.Daily, null, count, new DateOnly(2026, 11, 13)));

        var schedule = Assert.IsType<CalculatedSchedule>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(0m, schedule.InterestAmount);
        Assert.Equal(1000m, schedule.TotalAmount);
        Assert.Equal(count, schedule.Installments.Count);
        Assert.Equal(1000m, schedule.Installments.Sum(i => i.ExpectedAmount));
        Assert.Equal(count == 3 ? 333.34m : 250m, schedule.Installments[^1].ExpectedAmount);
    }

    [Fact]
    public void PreviewSchedule_negative_interest_is_rejected()
    {
        var controller = new LoansController(null!);
        var result = controller.PreviewSchedule(new PreviewLoanScheduleRequest(
            1000m, -1m, PaymentFrequency.Daily, null, 3, new DateOnly(2026, 11, 13)));

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
