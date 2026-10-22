using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.Backend.Api.Security;
using MS.Cobroup.Backend.Core.Payments;

namespace MS.Cobroup.Backend.Api.Controllers;

[ApiController]
[Route("api/operations/v1/payments")]
[Authorize(Policy = "tenant-user")]
public sealed class PaymentsController(
    PaymentService service,
    IOperationalPaymentMethodDirectory paymentMethodDirectory,
    Domain.Interfaces.Services.IFileStorageService fileStorageService,
    IPaymentRepository paymentRepository) : ControllerBase
{
    [HttpGet("payment-methods")]
    public async Task<IActionResult> ListPaymentMethods(CancellationToken cancellationToken)
    {
        var token = CustomerActorReader.ReadBearerToken(Request);
        var result = await paymentMethodDirectory.ListAvailableAsync(token, cancellationToken);
        return result.Outcome switch
        {
            PaymentMethodDirectoryOutcome.Success => Ok(result.Items),
            PaymentMethodDirectoryOutcome.Forbidden => Forbid(),
            PaymentMethodDirectoryOutcome.Unauthorized => Unauthorized(),
            _ => Problem("Directorio de medios de pago no disponible.", statusCode: StatusCodes.Status503ServiceUnavailable)
        };
    }
    [HttpPost]
    public async Task<IActionResult> Register(
        [FromBody] RegisterPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var input = new RegisterPaymentInput(
            request.LoanId,
            request.Amount,
            request.PaymentMethodId,
            request.Notes,
            request.IdempotencyKey);

        var result = await service.RegisterPaymentAsync(
            actor,
            input,
            CustomerActorReader.ReadBearerToken(Request),
            cancellationToken);

        return MapResult(result);
    }

    [HttpGet("by-loan/{loanId:guid}")]
    public async Task<IActionResult> ListByLoan(
        [FromRoute] Guid loanId,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        try
        {
            var payments = await service.ListByLoanAsync(
                actor,
                loanId,
                CustomerActorReader.ReadBearerToken(Request),
                cancellationToken);

            return Ok(payments);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpGet("daily-reconciliation")]
    public async Task<IActionResult> GetDailyReconciliation(
        [FromQuery] Guid routeId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        try
        {
            var report = await service.GetDailyReconciliationAsync(
                actor,
                routeId,
                targetDate,
                CustomerActorReader.ReadBearerToken(Request),
                cancellationToken);

            return Ok(report);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPost("{paymentId:guid}/evidence/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadEvidence(
        [FromRoute] Guid paymentId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "El archivo de imagen es requerido." });
        }

        await using var stream = file.OpenReadStream();
        var result = await service.StoreAndAttachEvidenceAsync(
            actor,
            paymentId,
            file.FileName,
            file.ContentType,
            stream,
            CustomerActorReader.ReadBearerToken(Request),
            cancellationToken);

        return MapResult(result);
    }

    [HttpGet("{paymentId:guid}/evidence/{evidenceId:guid}/download")]
    public async Task<IActionResult> DownloadEvidence(
        [FromRoute] Guid paymentId,
        [FromRoute] Guid evidenceId,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var evidence = await paymentRepository.FindEvidenceAsync(actor.TenantId, paymentId, evidenceId, cancellationToken);
        if (evidence is null || evidence.IsDeleted)
        {
            return NotFound();
        }

        var stream = await fileStorageService.ReadEvidenceAsync(actor.TenantId, evidence.StoragePath, cancellationToken);
        if (stream is null)
        {
            return NotFound();
        }

        return File(stream, evidence.ContentType, evidence.FileName);
    }

    [HttpPost("{paymentId:guid}/evidence")]
    public async Task<IActionResult> AttachEvidence(
        [FromRoute] Guid paymentId,
        [FromBody] AttachPaymentEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.AttachEvidenceAsync(
            actor,
            paymentId,
            request.FileName,
            request.ContentType,
            request.ByteSize,
            request.StoragePath,
            CustomerActorReader.ReadBearerToken(Request),
            cancellationToken);

        return MapResult(result);
    }

    [HttpDelete("{paymentId:guid}/evidence/{evidenceId:guid}")]
    public async Task<IActionResult> DeleteEvidence(
        [FromRoute] Guid paymentId,
        [FromRoute] Guid evidenceId,
        [FromBody] DeletePaymentEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.DeleteEvidenceAsync(
            actor,
            paymentId,
            evidenceId,
            request.Reason,
            cancellationToken);

        return MapResult(result);
    }

    private IActionResult MapResult(PaymentResult result)
    {
        if (result.Success)
        {
            return Ok(result.Payment);
        }

        return result.ErrorType switch
        {
            PaymentErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
            PaymentErrorType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage }),
            PaymentErrorType.ValidationError => BadRequest(new { message = result.ErrorMessage }),
            PaymentErrorType.LoanClosed => BadRequest(new { message = result.ErrorMessage }),
            PaymentErrorType.AmountExceedsBalance => BadRequest(new { message = result.ErrorMessage }),
            PaymentErrorType.InvalidPaymentMethod => BadRequest(new { message = result.ErrorMessage }),
            PaymentErrorType.Conflict => Conflict(new { message = result.ErrorMessage }),
            _ => Problem(result.ErrorMessage ?? "Error procesando el pago.")
        };
    }
}

public sealed record RegisterPaymentRequest(
    Guid LoanId,
    decimal Amount,
    Guid PaymentMethodId,
    string? Notes,
    string IdempotencyKey);

public sealed record AttachPaymentEvidenceRequest(
    string FileName,
    string ContentType,
    long ByteSize,
    string StoragePath);

public sealed record DeletePaymentEvidenceRequest(
    string Reason);
