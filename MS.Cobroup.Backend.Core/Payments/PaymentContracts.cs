using MS.Cobroup.Backend.Domain.Loans;
using MS.Cobroup.Backend.Domain.Payments;

namespace MS.Cobroup.Backend.Core.Payments;

public sealed record RegisterPaymentInput(
    Guid LoanId,
    decimal Amount,
    Guid PaymentMethodId,
    string? Notes,
    string IdempotencyKey);

public sealed record UpdatePaymentInput(
    decimal Amount,
    Guid PaymentMethodId,
    string? Notes);

public sealed record PaymentDetails(
    Guid Id,
    Guid LoanId,
    Guid CustomerId,
    Guid RouteId,
    decimal Amount,
    Guid PaymentMethodId,
    string PaymentMethodName,
    string? Notes,
    string IdempotencyKey,
    DateTimeOffset RegisteredAtUtc,
    decimal RemainingLoanBalance,
    LoanStatus LoanStatus,
    IReadOnlyCollection<PaymentAllocationItem> Allocations,
    IReadOnlyCollection<PaymentEvidenceItem> Evidences)
{
    public static PaymentDetails FromDomain(Payment payment, decimal loanBalance, LoanStatus loanStatus) =>
        new(
            payment.Id,
            payment.LoanId,
            payment.CustomerId,
            payment.RouteId,
            payment.Amount,
            payment.PaymentMethodId,
            payment.PaymentMethodName,
            payment.Notes,
            payment.IdempotencyKey,
            payment.RegisteredAtUtc,
            loanBalance,
            loanStatus,
            payment.Allocations.Select(PaymentAllocationItem.FromDomain).ToArray(),
            payment.Evidences.Select(PaymentEvidenceItem.FromDomain).ToArray());
}

public sealed record PaymentAllocationItem(
    Guid InstallmentId,
    int InstallmentNumber,
    decimal AmountAllocated,
    bool IsFullyCovered)
{
    public static PaymentAllocationItem FromDomain(PaymentAllocation allocation) =>
        new(
            allocation.InstallmentId,
            allocation.InstallmentNumber,
            allocation.AmountAllocated,
            allocation.IsFullyCovered);
}

public sealed record PaymentEvidenceItem(
    Guid Id,
    string FileName,
    string ContentType,
    long ByteSize,
    DateTimeOffset UploadedAtUtc,
    bool IsDeleted)
{
    public static PaymentEvidenceItem FromDomain(PaymentEvidence evidence) =>
        new(
            evidence.Id,
            evidence.FileName,
            evidence.ContentType,
            evidence.ByteSize,
            evidence.UploadedAtUtc,
            evidence.IsDeleted);
}

public sealed record DailyReconciliationReport(
    Guid RouteId,
    DateOnly Date,
    decimal TotalCollected,
    int PaymentCount,
    IReadOnlyCollection<PaymentMethodReconciliationSummary> MethodBreakdown);

public sealed record PaymentMethodReconciliationSummary(
    Guid PaymentMethodId,
    string PaymentMethodName,
    decimal TotalAmount,
    int Count);

public sealed record PaymentResult(
    bool Success,
    string? ErrorMessage,
    PaymentDetails? Payment,
    PaymentErrorType? ErrorType = null)
{
    public static PaymentResult Ok(PaymentDetails details) =>
        new(true, null, details);

    public static PaymentResult Failure(string error, PaymentErrorType errorType) =>
        new(false, error, null, errorType);
}

public enum PaymentErrorType
{
    NotFound,
    Forbidden,
    ValidationError,
    LoanClosed,
    AmountExceedsBalance,
    InvalidPaymentMethod,
    Conflict
}
