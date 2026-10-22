using Microsoft.EntityFrameworkCore;
using MS.Cobroup.Backend.Domain.Payments;
using MS.Cobroup.Backend.Infrastructure.Persistence;

namespace MS.Cobroup.Backend.Infrastructure.Tests.Persistence;

public sealed class EfPaymentRepositoryTests
{
    [Fact]
    public async Task FindByIdempotencyKey_respects_tenant_isolation()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var loanId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var collectorId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        var payment = Payment.Register(
            tenantId,
            loanId,
            customerId,
            routeId,
            collectorId,
            150m,
            paymentMethodId,
            "Efectivo",
            "Recaudo cuota",
            idempotencyKey,
            now,
            [new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 150m, true)]);

        var repository = new EfPaymentRepository(context);
        await repository.AddAsync(payment, default);
        await repository.SaveChangesAsync(default);
        context.ChangeTracker.Clear();

        var found = await repository.FindByIdempotencyKeyAsync(tenantId, idempotencyKey, default);
        Assert.NotNull(found);
        Assert.Equal(payment.Id, found.Id);
        Assert.Equal(150m, found.Amount);
        Assert.Single(found.Allocations);

        var otherTenantFound = await repository.FindByIdempotencyKeyAsync(Guid.NewGuid(), idempotencyKey, default);
        Assert.Null(otherTenantFound);
    }

    [Fact]
    public async Task ListByLoan_returns_payments_ordered_descending()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var loanId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var collectorId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var p1 = Payment.Register(
            tenantId,
            loanId,
            customerId,
            routeId,
            collectorId,
            100m,
            paymentMethodId,
            "Efectivo",
            null,
            Guid.NewGuid().ToString(),
            now.AddMinutes(-10),
            [new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 100m, true)]);

        var p2 = Payment.Register(
            tenantId,
            loanId,
            customerId,
            routeId,
            collectorId,
            200m,
            paymentMethodId,
            "Transferencia",
            null,
            Guid.NewGuid().ToString(),
            now,
            [new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, 200m, true)]);

        var repository = new EfPaymentRepository(context);
        await repository.AddAsync(p1, default);
        await repository.AddAsync(p2, default);
        await repository.SaveChangesAsync(default);
        context.ChangeTracker.Clear();

        var list = await repository.ListByLoanAsync(tenantId, loanId, default);
        Assert.Equal(2, list.Count);
        Assert.Equal(p2.Id, list.First().Id);
    }

    [Fact]
    public async Task ListByRouteAndDate_filters_by_route_and_date_range()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var routeA = Guid.NewGuid();
        var routeB = Guid.NewGuid();
        var collectorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var paymentA = Payment.Register(
            tenantId,
            Guid.NewGuid(),
            customerId,
            routeA,
            collectorId,
            300m,
            Guid.NewGuid(),
            "Efectivo",
            null,
            Guid.NewGuid().ToString(),
            now,
            [new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 300m, true)]);

        var paymentB = Payment.Register(
            tenantId,
            Guid.NewGuid(),
            customerId,
            routeB,
            collectorId,
            500m,
            Guid.NewGuid(),
            "Efectivo",
            null,
            Guid.NewGuid().ToString(),
            now,
            [new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 500m, true)]);

        var repository = new EfPaymentRepository(context);
        await repository.AddAsync(paymentA, default);
        await repository.AddAsync(paymentB, default);
        await repository.SaveChangesAsync(default);
        context.ChangeTracker.Clear();

        var routeAPayments = await repository.ListByRouteAndDateAsync(
            tenantId,
            routeA,
            now.AddHours(-1),
            now.AddHours(1),
            default);

        var singlePayment = Assert.Single(routeAPayments);
        Assert.Equal(paymentA.Id, singlePayment.Id);
        Assert.Equal(300m, singlePayment.Amount);
    }

    [Fact]
    public async Task AddEvidence_and_FindEvidence_work_correctly()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var evidence = PaymentEvidence.Create(
            paymentId,
            tenantId,
            "rec-001.jpg",
            "image/jpeg",
            2048,
            "/storage/evidences/rec-001.jpg",
            Guid.NewGuid(),
            now);

        var repository = new EfPaymentRepository(context);
        await repository.AddEvidenceAsync(evidence, default);
        context.ChangeTracker.Clear();

        var found = await repository.FindEvidenceAsync(tenantId, paymentId, evidence.Id, default);
        Assert.NotNull(found);
        Assert.Equal(evidence.StoragePath, found.StoragePath);
        Assert.False(found.IsDeleted);

        found.MarkDeleted(Guid.NewGuid(), "Duplicate photo uploaded", now.AddMinutes(5));
        await repository.SaveChangesAsync(default);
        context.ChangeTracker.Clear();

        var updated = await repository.FindEvidenceAsync(tenantId, paymentId, evidence.Id, default);
        Assert.NotNull(updated);
        Assert.True(updated.IsDeleted);
        Assert.Equal("Duplicate photo uploaded", updated.DeletionReason);
    }

    private static OperationsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OperationsDbContext(options);
    }
}
