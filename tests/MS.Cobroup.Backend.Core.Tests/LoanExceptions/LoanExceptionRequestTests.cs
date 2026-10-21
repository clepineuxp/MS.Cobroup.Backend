using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.LoanExceptions;

namespace MS.Cobroup.Backend.Core.Tests.LoanExceptions;

public sealed class LoanExceptionRequestTests
{
    [Fact]
    public void Create_initializes_pending_request_with_normalized_terms()
    {
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = LoanExceptionRequest.Create(
            tenantId,
            customerId,
            routeId,
            50000.555m,
            20,
            15.5m,
            "  Cliente preferencial con aval  ",
            creatorId,
            UserRole.Collector,
            now);

        Assert.Equal(tenantId, request.TenantId);
        Assert.Equal(customerId, request.CustomerId);
        Assert.Equal(routeId, request.RouteId);
        Assert.Equal(50000.56m, request.ProposedAmount);
        Assert.Equal(20, request.ProposedDays);
        Assert.Equal(15.50m, request.ProposedPercentage);
        Assert.Equal("Cliente preferencial con aval", request.Reason);
        Assert.Equal(LoanExceptionStatus.Pending, request.Status);
        Assert.Equal(creatorId, request.CreatedByUserId);
        Assert.Equal(UserRole.Collector, request.CreatedByRole);
        Assert.Equal(now, request.CreatedAtUtc);
        Assert.Null(request.DecidedByUserId);
        Assert.Null(request.DecidedAtUtc);
        Assert.Null(request.DecisionNotes);
        Assert.Null(request.CancelledByUserId);
        Assert.Null(request.CancelledAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Create_rejects_non_positive_amount(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoanExceptionRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            amount,
            15,
            10m,
            "Motivo",
            Guid.NewGuid(),
            UserRole.Collector,
            DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_rejects_non_positive_days(int days)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoanExceptionRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1000m,
            days,
            10m,
            "Motivo",
            Guid.NewGuid(),
            UserRole.Collector,
            DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Create_rejects_non_positive_percentage(decimal percentage)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoanExceptionRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1000m,
            15,
            percentage,
            "Motivo",
            Guid.NewGuid(),
            UserRole.Collector,
            DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_rejects_empty_reason(string? reason)
    {
        Assert.Throws<ArgumentException>(() => LoanExceptionRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1000m,
            15,
            10m,
            reason!,
            Guid.NewGuid(),
            UserRole.Collector,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ApplyDecision_approves_pending_request()
    {
        var request = CreateValid();
        var adminId = Guid.NewGuid();
        var decidedAt = DateTimeOffset.UtcNow;

        var modified = request.ApplyDecision(
            LoanExceptionDecision.Approved,
            adminId,
            "Aprobado por excelente historial",
            decidedAt);

        Assert.True(modified);
        Assert.Equal(LoanExceptionStatus.Approved, request.Status);
        Assert.Equal(adminId, request.DecidedByUserId);
        Assert.Equal(decidedAt, request.DecidedAtUtc);
        Assert.Equal("Aprobado por excelente historial", request.DecisionNotes);
    }

    [Fact]
    public void ApplyDecision_rejects_pending_request()
    {
        var request = CreateValid();
        var adminId = Guid.NewGuid();
        var decidedAt = DateTimeOffset.UtcNow;

        var modified = request.ApplyDecision(
            LoanExceptionDecision.Rejected,
            adminId,
            "Riesgo crediticio elevado",
            decidedAt);

        Assert.True(modified);
        Assert.Equal(LoanExceptionStatus.Rejected, request.Status);
        Assert.Equal(adminId, request.DecidedByUserId);
        Assert.Equal(decidedAt, request.DecidedAtUtc);
        Assert.Equal("Riesgo crediticio elevado", request.DecisionNotes);
    }

    [Fact]
    public void ApplyDecision_is_idempotent_when_called_again_with_same_outcome()
    {
        var request = CreateValid();
        var adminId = Guid.NewGuid();
        var decidedAt = DateTimeOffset.UtcNow;

        request.ApplyDecision(LoanExceptionDecision.Approved, adminId, "OK", decidedAt);
        var secondCall = request.ApplyDecision(LoanExceptionDecision.Approved, adminId, "OK", decidedAt);

        Assert.False(secondCall);
        Assert.Equal(LoanExceptionStatus.Approved, request.Status);
    }

    [Fact]
    public void ApplyDecision_throws_if_already_in_different_final_state()
    {
        var request = CreateValid();
        var adminId = Guid.NewGuid();

        request.ApplyDecision(LoanExceptionDecision.Approved, adminId, null, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            request.ApplyDecision(LoanExceptionDecision.Rejected, adminId, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Cancel_cancels_pending_request()
    {
        var request = CreateValid();
        var cancellerId = Guid.NewGuid();
        var cancelledAt = DateTimeOffset.UtcNow;

        var modified = request.Cancel(cancellerId, cancelledAt);

        Assert.True(modified);
        Assert.Equal(LoanExceptionStatus.Cancelled, request.Status);
        Assert.Equal(cancellerId, request.CancelledByUserId);
        Assert.Equal(cancelledAt, request.CancelledAtUtc);
    }

    [Fact]
    public void Cancel_throws_if_request_not_pending()
    {
        var request = CreateValid();
        request.ApplyDecision(LoanExceptionDecision.Approved, Guid.NewGuid(), null, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            request.Cancel(Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    private static LoanExceptionRequest CreateValid() =>
        LoanExceptionRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            20000m,
            30,
            12m,
            "Condiciones especiales autorizadas",
            Guid.NewGuid(),
            UserRole.Collector,
            DateTimeOffset.UtcNow);
}
