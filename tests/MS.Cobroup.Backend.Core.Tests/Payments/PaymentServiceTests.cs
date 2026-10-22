using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Core.Payments;
using MS.Cobroup.Backend.Core.Routes;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.Interfaces.Services;
using MS.Cobroup.Backend.Domain.Loans;
using MS.Cobroup.Backend.Domain.Payments;

namespace MS.Cobroup.Backend.Core.Tests.Payments;

public sealed class PaymentServiceTests
{
    private readonly InMemoryPaymentRepository paymentRepository = new();
    private readonly InMemoryLoanRepository loanRepository = new();
    private readonly FakeRouteDirectory routeDirectory = new();
    private readonly FakePaymentMethodDirectory paymentMethodDirectory = new();
    private readonly FakeFileStorageService fileStorageService = new();
    private readonly PaymentService service;

    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid routeId = Guid.NewGuid();
    private readonly Guid collectorId = Guid.NewGuid();
    private readonly Guid paymentMethodId = Guid.NewGuid();

    public PaymentServiceTests()
    {
        service = new PaymentService(
            paymentRepository,
            loanRepository,
            routeDirectory,
            paymentMethodDirectory,
            fileStorageService);

        routeDirectory.Routes = [new(routeId, "Ruta Centro", null, "Cobrador 1", 10, 10)];
        paymentMethodDirectory.Methods = [new(paymentMethodId, "Efectivo", 1)];
    }

    [Fact]
    public async Task RegisterPaymentAsync_fails_when_idempotency_key_is_missing()
    {
        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var input = new RegisterPaymentInput(Guid.NewGuid(), 100m, paymentMethodId, null, "   ");

        var result = await service.RegisterPaymentAsync(actor, input, "token", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PaymentErrorType.ValidationError, result.ErrorType);
        Assert.Equal("La clave de idempotencia es requerida.", result.ErrorMessage);
    }

    [Fact]
    public async Task RegisterPaymentAsync_returns_existing_payment_on_duplicate_idempotency_key()
    {
        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var loan = CreateSampleLoan(1000m, 2);
        loanRepository.Add(loan);

        var input = new RegisterPaymentInput(loan.Id, 200m, paymentMethodId, "Primer pago", "KEY-12345");
        var firstResult = await service.RegisterPaymentAsync(actor, input, "token", CancellationToken.None);
        Assert.True(firstResult.Success);

        // Re-call with same idempotency key (AC-12)
        var duplicateResult = await service.RegisterPaymentAsync(actor, input, "token", CancellationToken.None);
        Assert.True(duplicateResult.Success);
        Assert.Equal(firstResult.Payment!.Id, duplicateResult.Payment!.Id);
        Assert.Equal("KEY-12345", duplicateResult.Payment.IdempotencyKey);
    }

    [Fact]
    public async Task RegisterPaymentAsync_fails_when_amount_is_zero_or_negative()
    {
        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var loan = CreateSampleLoan(1000m, 2);
        loanRepository.Add(loan);

        var input = new RegisterPaymentInput(loan.Id, 0m, paymentMethodId, null, "KEY-ZERO");
        var result = await service.RegisterPaymentAsync(actor, input, "token", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PaymentErrorType.ValidationError, result.ErrorType);
    }

    [Fact]
    public async Task RegisterPaymentAsync_fails_when_amount_exceeds_loan_balance()
    {
        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var loan = CreateSampleLoan(500m, 2);
        loanRepository.Add(loan);

        var input = new RegisterPaymentInput(loan.Id, 600m, paymentMethodId, null, "KEY-EXCEED");
        var result = await service.RegisterPaymentAsync(actor, input, "token", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PaymentErrorType.AmountExceedsBalance, result.ErrorType);
        Assert.Contains("supera el saldo pendiente", result.ErrorMessage);
    }

    [Fact]
    public async Task RegisterPaymentAsync_fails_when_payment_method_is_invalid()
    {
        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var loan = CreateSampleLoan(1000m, 2);
        loanRepository.Add(loan);

        var invalidMethodId = Guid.NewGuid();
        var input = new RegisterPaymentInput(loan.Id, 100m, invalidMethodId, null, "KEY-BAD-METHOD");
        var result = await service.RegisterPaymentAsync(actor, input, "token", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PaymentErrorType.InvalidPaymentMethod, result.ErrorType);
    }

    [Fact]
    public async Task RegisterPaymentAsync_cascades_allocation_across_installments_and_completes_loan_when_fully_paid()
    {
        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        // Loan with 2 installments of 500 each = 1000 total
        var loan = CreateSampleLoan(1000m, 2);
        loanRepository.Add(loan);

        // Payment 1: 750 (covers installment 1 in full [500] and covers 250 of installment 2)
        var input1 = new RegisterPaymentInput(loan.Id, 750m, paymentMethodId, "Pago parcial 1", "KEY-PARTIAL-1");
        var result1 = await service.RegisterPaymentAsync(actor, input1, "token", CancellationToken.None);

        Assert.True(result1.Success);
        Assert.Equal(250m, result1.Payment!.RemainingLoanBalance);
        Assert.Equal(LoanStatus.Active, result1.Payment.LoanStatus);
        Assert.Equal(2, result1.Payment.Allocations.Count);

        var alloc1 = result1.Payment.Allocations.First(a => a.InstallmentNumber == 1);
        Assert.Equal(500m, alloc1.AmountAllocated);
        Assert.True(alloc1.IsFullyCovered);

        var alloc2 = result1.Payment.Allocations.First(a => a.InstallmentNumber == 2);
        Assert.Equal(250m, alloc2.AmountAllocated);
        Assert.False(alloc2.IsFullyCovered);

        // Payment 2: Remaining 250 (covers the rest of installment 2, finishes loan)
        var input2 = new RegisterPaymentInput(loan.Id, 250m, paymentMethodId, "Pago liquidación", "KEY-PARTIAL-2");
        var result2 = await service.RegisterPaymentAsync(actor, input2, "token", CancellationToken.None);

        Assert.True(result2.Success);
        Assert.Equal(0m, result2.Payment!.RemainingLoanBalance);
        Assert.Equal(LoanStatus.PaidOff, result2.Payment.LoanStatus);
        Assert.Single(result2.Payment.Allocations);
        Assert.True(result2.Payment.Allocations.First().IsFullyCovered);
        Assert.Equal(250m, result2.Payment.Allocations.First().AmountAllocated);
    }

    [Fact]
    public async Task GetDailyReconciliationAsync_groups_payments_by_payment_method()
    {
        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var loan = CreateSampleLoan(2000m, 4);
        loanRepository.Add(loan);

        var transferMethodId = Guid.NewGuid();
        paymentMethodDirectory.Methods =
        [
            new(paymentMethodId, "Efectivo", 1),
            new(transferMethodId, "Transferencia Bancaria", 2)
        ];

        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota"); }
        catch { tz = TimeZoneInfo.Utc; }
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));

        // 2 payments with Efectivo (200 + 300 = 500)
        await service.RegisterPaymentAsync(actor, new RegisterPaymentInput(loan.Id, 200m, paymentMethodId, null, "P-1"), "token", CancellationToken.None);
        await service.RegisterPaymentAsync(actor, new RegisterPaymentInput(loan.Id, 300m, paymentMethodId, null, "P-2"), "token", CancellationToken.None);

        // 1 payment with Transferencia (400)
        await service.RegisterPaymentAsync(actor, new RegisterPaymentInput(loan.Id, 400m, transferMethodId, null, "P-3"), "token", CancellationToken.None);

        var reconciliation = await service.GetDailyReconciliationAsync(actor, routeId, today, "token", CancellationToken.None);

        Assert.Equal(900m, reconciliation.TotalCollected);
        Assert.Equal(3, reconciliation.PaymentCount);
        Assert.Equal(2, reconciliation.MethodBreakdown.Count);

        var efectivoSummary = reconciliation.MethodBreakdown.First(b => b.PaymentMethodName == "Efectivo");
        Assert.Equal(500m, efectivoSummary.TotalAmount);
        Assert.Equal(2, efectivoSummary.Count);

        var transferSummary = reconciliation.MethodBreakdown.First(b => b.PaymentMethodName == "Transferencia Bancaria");
        Assert.Equal(400m, transferSummary.TotalAmount);
        Assert.Single(reconciliation.MethodBreakdown, b => b.PaymentMethodName == "Transferencia Bancaria");
    }

    [Fact]
    public async Task StoreAndAttachEvidenceAsync_fails_for_disallowed_content_type()
    {
        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var loan = CreateSampleLoan(1000m, 2);
        loanRepository.Add(loan);

        var paymentResult = await service.RegisterPaymentAsync(actor, new RegisterPaymentInput(loan.Id, 100m, paymentMethodId, null, "P-EVID"), "token", CancellationToken.None);

        using var stream = new MemoryStream("%PDF-1.4"u8.ToArray());
        var result = await service.StoreAndAttachEvidenceAsync(
            actor,
            paymentResult.Payment!.Id,
            "recibo.pdf",
            "application/pdf",
            stream,
            "token",
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PaymentErrorType.ValidationError, result.ErrorType);
        Assert.Contains("Formato de imagen no permitido", result.ErrorMessage);
    }

    [Fact]
    public async Task Evidence_deletion_is_restricted_to_admin()
    {
        var collectorActor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var adminActor = new CustomerActor(Guid.NewGuid(), tenantId, UserRole.Admin);
        var loan = CreateSampleLoan(1000m, 2);
        loanRepository.Add(loan);

        var paymentResult = await service.RegisterPaymentAsync(collectorActor, new RegisterPaymentInput(loan.Id, 100m, paymentMethodId, null, "P-EVID-DEL"), "token", CancellationToken.None);

        using var validJpegStream = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46]);
        var attachResult = await service.StoreAndAttachEvidenceAsync(
            collectorActor,
            paymentResult.Payment!.Id,
            "recibo.jpg",
            "image/jpeg",
            validJpegStream,
            "token",
            CancellationToken.None);
        Assert.True(attachResult.Success);

        var evidenceId = paymentRepository.Evidences.First().Id;

        // Attempt deletion by Collector -> Fails (AC-14)
        var collectorDeleteResult = await service.DeleteEvidenceAsync(collectorActor, paymentResult.Payment.Id, evidenceId, "Foto borrosa", CancellationToken.None);
        Assert.False(collectorDeleteResult.Success);
        Assert.Equal(PaymentErrorType.Forbidden, collectorDeleteResult.ErrorType);

        // Deletion by Admin with empty reason -> Fails
        var emptyReasonResult = await service.DeleteEvidenceAsync(adminActor, paymentResult.Payment.Id, evidenceId, "   ", CancellationToken.None);
        Assert.False(emptyReasonResult.Success);
        Assert.Equal(PaymentErrorType.ValidationError, emptyReasonResult.ErrorType);

        // Deletion by Admin with valid reason -> Succeeds
        var adminDeleteResult = await service.DeleteEvidenceAsync(adminActor, paymentResult.Payment.Id, evidenceId, "Comprobante duplicado por error de carga", CancellationToken.None);
        Assert.True(adminDeleteResult.Success);

        var deletedEvidence = paymentRepository.Evidences.First(e => e.Id == evidenceId);
        Assert.True(deletedEvidence.IsDeleted);
        Assert.Equal("Comprobante duplicado por error de carga", deletedEvidence.DeletionReason);
        Assert.Equal(adminActor.UserId, deletedEvidence.DeletedByUserId);
    }

    private Loan CreateSampleLoan(decimal totalAmount, int installmentCount)
    {
        var now = DateTimeOffset.UtcNow;
        var terms = LoanTermsSnapshot.Create(
            totalAmount * 0.8m,
            20m,
            totalAmount * 0.2m,
            totalAmount,
            PaymentFrequency.Daily,
            null,
            installmentCount,
            now,
            now.AddDays(installmentCount));

        var perInstallment = totalAmount / installmentCount;
        var installments = Enumerable.Range(1, installmentCount)
            .Select(i => Installment.Create(tenantId, Guid.NewGuid(), i, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(i)), perInstallment))
            .ToList();

        return Loan.Originate(
            tenantId,
            Guid.NewGuid(),
            routeId,
            null,
            null,
            null,
            terms,
            collectorId,
            UserRole.Collector,
            now,
            installments);
    }

    private sealed class InMemoryPaymentRepository : IPaymentRepository
    {
        public List<Payment> Payments { get; } = [];
        public List<PaymentEvidence> Evidences { get; } = [];

        public Task<Payment?> FindByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult(Payments.SingleOrDefault(p => p.TenantId == tenantId && p.IdempotencyKey == idempotencyKey));

        public Task<Payment?> FindAsync(Guid tenantId, Guid paymentId, CancellationToken cancellationToken) =>
            Task.FromResult(Payments.SingleOrDefault(p => p.TenantId == tenantId && p.Id == paymentId));

        public Task<IReadOnlyCollection<Payment>> ListByLoanAsync(Guid tenantId, Guid loanId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Payment>>(Payments.Where(p => p.TenantId == tenantId && p.LoanId == loanId).ToList());

        public Task<IReadOnlyCollection<Payment>> ListByRouteAndDateAsync(Guid tenantId, Guid routeId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Payment>>(Payments.Where(p => p.TenantId == tenantId && p.RouteId == routeId && p.RegisteredAtUtc >= fromUtc && p.RegisteredAtUtc <= toUtc).ToList());

        public Task AddAsync(Payment payment, CancellationToken cancellationToken)
        {
            Payments.Add(payment);
            return Task.CompletedTask;
        }

        public Task<PaymentSaveOutcome> TryRegisterPaymentAsync(
            Payment payment,
            Loan loan,
            CancellationToken cancellationToken)
        {
            if (Payments.Any(p => p.TenantId == payment.TenantId && p.IdempotencyKey == payment.IdempotencyKey))
            {
                return Task.FromResult(PaymentSaveOutcome.IdempotentReplay);
            }

            Payments.Add(payment);
            return Task.FromResult(PaymentSaveOutcome.Success);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<PaymentEvidence?> FindEvidenceAsync(Guid tenantId, Guid paymentId, Guid evidenceId, CancellationToken cancellationToken) =>
            Task.FromResult(Evidences.SingleOrDefault(e => e.TenantId == tenantId && e.PaymentId == paymentId && e.Id == evidenceId));

        public Task AddEvidenceAsync(PaymentEvidence evidence, CancellationToken cancellationToken)
        {
            Evidences.Add(evidence);
            var payment = Payments.SingleOrDefault(p => p.Id == evidence.PaymentId);
            if (payment is not null && !payment.Evidences.Contains(evidence))
            {
                payment.AddEvidence(evidence);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryLoanRepository : ILoanRepository
    {
        private readonly List<Loan> loans = [];

        public void Add(Loan loan) => loans.Add(loan);

        public Task<Loan?> FindAsync(Guid tenantId, Guid loanId, CancellationToken cancellationToken) =>
            Task.FromResult(loans.SingleOrDefault(l => l.TenantId == tenantId && l.Id == loanId));

        public Task AddAsync(Loan loan, CancellationToken cancellationToken)
        {
            loans.Add(loan);
            return Task.CompletedTask;
        }

        public Task<LoanSaveOutcome> TryAddAsync(
            Loan loan,
            int? maxActiveLoans,
            Guid? consumedExceptionId,
            CancellationToken cancellationToken)
        {
            if (maxActiveLoans.HasValue)
            {
                var active = loans.Count(l => l.TenantId == loan.TenantId && l.RouteId == loan.RouteId && l.Status == LoanStatus.Active);
                if (active >= maxActiveLoans.Value)
                {
                    return Task.FromResult(LoanSaveOutcome.CapacityReached);
                }
            }

            loans.Add(loan);
            return Task.FromResult(LoanSaveOutcome.Saved);
        }

        public Task<int> CountActiveLoansByRouteAsync(Guid tenantId, Guid routeId, CancellationToken cancellationToken) =>
            Task.FromResult(loans.Count(l => l.TenantId == tenantId && l.RouteId == routeId && l.Status == LoanStatus.Active));

        public Task<IReadOnlyCollection<Loan>> ListByCustomerAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Loan>>(loans.Where(l => l.TenantId == tenantId && l.CustomerId == customerId).ToList());

        public Task<LoanSearchPage> SearchAsync(Guid tenantId, IReadOnlyCollection<Guid> accessibleRouteIds, Guid? routeId, Guid? customerId, LoanStatus? status, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new LoanSearchPage([], page, pageSize, 0));

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeRouteDirectory : IAdministrativeRouteDirectory
    {
        public IReadOnlyCollection<AccessibleRoute> Routes { get; set; } = [];
        public Task<RouteDirectoryResult> ListAccessibleAsync(string accessToken, CancellationToken cancellationToken) =>
            Task.FromResult(RouteDirectoryResult.Success(Routes));
    }

    private sealed class FakePaymentMethodDirectory : IOperationalPaymentMethodDirectory
    {
        public IReadOnlyCollection<OperationalPaymentMethodItem> Methods { get; set; } = [];
        public Task<PaymentMethodDirectoryResult> ListAvailableAsync(string accessToken, CancellationToken cancellationToken) =>
            Task.FromResult(PaymentMethodDirectoryResult.Success(Methods));
    }

    private sealed class FakeFileStorageService : IFileStorageService
    {
        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        public Task<StoredFileResult> StoreEvidenceAsync(
            Guid tenantId,
            Guid paymentId,
            string fileName,
            string declaredContentType,
            Stream stream,
            CancellationToken cancellationToken)
        {
            if (!AllowedMimeTypes.Contains(declaredContentType))
            {
                throw new InvalidOperationException("Formato de imagen no permitido. Solo se aceptan JPEG, PNG y WebP.");
            }
            return Task.FromResult(new StoredFileResult("evidence/test.jpg", declaredContentType, stream.Length, "sha256-test-hash"));
        }

        public Task<Stream?> ReadEvidenceAsync(
            Guid tenantId,
            string storagePath,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<Stream?>(new MemoryStream([1, 2, 3]));
        }

        public Task DeleteEvidenceAsync(
            Guid tenantId,
            string storagePath,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
