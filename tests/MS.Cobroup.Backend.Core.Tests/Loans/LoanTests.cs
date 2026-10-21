using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Loans;

namespace MS.Cobroup.Backend.Core.Tests.Loans;

public sealed class LoanTests
{
    [Fact]
    public void Originate_creates_active_loan_when_installments_sum_matches_total()
    {
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var terms = LoanTermsSnapshot.Create(
            1000m,
            10m,
            100m,
            1100m,
            PaymentFrequency.Daily,
            null,
            2,
            now,
            now.AddDays(2));

        var installments = new List<Installment>
        {
            Installment.Create(tenantId, Guid.NewGuid(), 1, DateOnly.FromDateTime(DateTime.UtcNow), 550m),
            Installment.Create(tenantId, Guid.NewGuid(), 2, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), 550m)
        };

        var loan = Loan.Originate(
            tenantId,
            customerId,
            routeId,
            null,
            null,
            null,
            terms,
            userId,
            UserRole.Collector,
            now,
            installments);

        Assert.NotEqual(Guid.Empty, loan.Id);
        Assert.Equal(LoanStatus.Active, loan.Status);
        Assert.Equal(2, loan.Installments.Count);
        Assert.Equal(1100m, loan.Terms.TotalAmount);
        Assert.Equal(1100m, loan.Terms.BalanceAmount);
        Assert.Equal(0m, loan.Terms.PaidAmount);
    }

    [Fact]
    public void Originate_throws_when_installments_sum_does_not_match_total_amount()
    {
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var terms = LoanTermsSnapshot.Create(
            1000m,
            10m,
            100m,
            1100m,
            PaymentFrequency.Daily,
            null,
            2,
            now,
            now.AddDays(2));

        // Installments sum to 1099m != 1100m
        var installments = new List<Installment>
        {
            Installment.Create(tenantId, Guid.NewGuid(), 1, DateOnly.FromDateTime(DateTime.UtcNow), 550m),
            Installment.Create(tenantId, Guid.NewGuid(), 2, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), 549m)
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            Loan.Originate(
                tenantId,
                customerId,
                routeId,
                null,
                null,
                null,
                terms,
                userId,
                UserRole.Collector,
                now,
                installments));

        Assert.Contains("must match total amount", ex.Message);
    }

    [Fact]
    public void Originate_throws_when_installment_count_mismatches_terms()
    {
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var terms = LoanTermsSnapshot.Create(
            1000m,
            10m,
            100m,
            1100m,
            PaymentFrequency.Daily,
            null,
            3, // Expected 3
            now,
            now.AddDays(3));

        var installments = new List<Installment>
        {
            Installment.Create(tenantId, Guid.NewGuid(), 1, DateOnly.FromDateTime(DateTime.UtcNow), 550m),
            Installment.Create(tenantId, Guid.NewGuid(), 2, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), 550m)
        };

        Assert.Throws<ArgumentException>(() =>
            Loan.Originate(
                tenantId,
                customerId,
                routeId,
                null,
                null,
                null,
                terms,
                userId,
                UserRole.Collector,
                now,
                installments));
    }
}
