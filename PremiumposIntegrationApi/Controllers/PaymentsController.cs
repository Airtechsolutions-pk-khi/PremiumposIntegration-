using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PremiumposIntegrationApi.Services;

namespace PremiumposIntegrationApi.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly PaymentService _paymentService;
    private readonly MoyassarClient _moyassarClient;

    public PaymentsController(PaymentService paymentService, MoyassarClient moyassarClient)
    {
        _paymentService = paymentService;
        _moyassarClient = moyassarClient;
    }


    [HttpPost("invoice/create")]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request)
    {
        try
        {
            var result = await _paymentService.CreateInvoiceForOrderAsync(
                request.OrderId,
                request.Amount,
                request.Currency ?? "SAR",
                request.SuccessUrl,
                request.BackUrl);

            return Ok(new
            {
                success = true,
                status = "initiated",
                paymentId = result.PaymentId,
                redirectUrl = result.RedirectUrl
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                status = "failed",
                message = "Could not initiate payment.",
                error = ex.Message
            });
        }
    }

    [HttpGet("status/{orderId}")]
    public async Task<IActionResult> GetStatus(string orderId)
    {
        var payment = await _paymentService.GetLatestPaymentForOrderAsync(orderId);
        if (payment == null) return NotFound();

        return Ok(new
        {
            orderId,
            status = payment.Status.ToString().ToLower(), // "initiated" | "pending" | "paid" | "failed" | "expired" | "cancelled"
            paymentId = payment.Id
        });
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        var body = ms.ToArray();

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        // Verify the secret_token embedded in the body, not a header.
        if (!_moyassarClient.VerifyWebhookSecret(root))
            return Unauthorized();

        var providerId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        if (providerId is null) return Ok(); // nothing we can act on

        // Do NOT trust the status in the webhook body directly — re-fetch live from Moyasar.
        // This protects against a stale/replayed event and against the fact that Moyasar's
        // webhook auth (shared secret_token) proves the sender knew the secret, not that
        // the body wasn't altered in transit — a live fetch is the actual source of truth.
        var verified = await _moyassarClient.FetchInvoiceAsync(providerId);

        await _paymentService.SyncFromVerifiedInvoiceAsync(verified);

        return Ok();
    }

    public record CreateInvoiceRequest(
    string OrderId,
    decimal Amount,
    string? Currency,
    string? SuccessUrl,
    string? BackUrl);

    [HttpPost("refund")]
    public async Task<IActionResult> Refund([FromBody] RefundRequest request)
    {
        try
        {
            var result = await _paymentService.RefundPaymentAsync(request.OrderId, request.ProviderPaymentId, request.Reason);

            return Ok(new
            {
                success = true,
                status = "refunding",
                message = "Refund initiated"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Refund failed",
                error = ex.Message
            });
        }
    }

    public record RefundRequest(string OrderId, string? ProviderPaymentId, string Reason);

    //[HttpPost("create")]
    //public async Task<IActionResult> Create([FromBody] CreatePaymentRequest request)
    //{
    //    try
    //    {
    //        var tenantId = "Test-Tenantid";// User.FindFirstValue(ClaimTypes.NameIdentifier);
    //        if (string.IsNullOrWhiteSpace(tenantId))
    //        {
    //            return Unauthorized(new { success = false, status = "failed", message = "Unauthorized request. Send a valid X-Secret-Key header." });
    //        }

    //        var result = await _paymentService.CreatePaymentForOrderAsync(
    //            request.OrderId,
    //            request.Amount,
    //            request.ReturnUrl,
    //            tenantId,
    //            request.CardDetails);

    //        var response = new
    //        {
    //            success = result.IsSuccess,
    //            status = result.IsSuccess ? "pending" : "failed",
    //            message = result.IsSuccess ? "Payment initiated successfully." : result.ErrorMessage ?? "Payment initiation failed.",
    //            paymentId = result.Payment.Id,
    //            providerPaymentId = result.Payment.ProviderPaymentId,
    //            redirectUrl = result.RedirectUrl,
    //            serialLog = result.SerializedLog
    //        };

    //        return Ok(response);
    //    }
    //    catch (Exception ex)
    //    {
    //        var fallback = new
    //        {
    //            success = false,
    //            status = "failed",
    //            message = "Payment initiation could not be completed.",
    //            serialLog = JsonSerializer.Serialize(new { @event = "payment.create.exception", error = ex.Message, timestamp = DateTime.UtcNow })
    //        };

    //        return StatusCode(500, fallback);
    //    }
    //}

    //[HttpPost("webhook")]
    //[AllowAnonymous]
    //public async Task<IActionResult> Webhook()
    //{
    //    using var ms = new MemoryStream();
    //    await Request.Body.CopyToAsync(ms);
    //    var body = ms.ToArray();
    //    var signatureHeader = Request.Headers["X-Moyassar-Signature"].ToString();

    //    if (!_moyassarClient.VerifyWebhook(body, signatureHeader))
    //    {
    //        return Unauthorized();
    //    }

    //    using var json = JsonDocument.Parse(body);
    //    var providerId = json.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
    //    var status = json.RootElement.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;

    //    if (providerId is not null && status == "paid")
    //    {
    //        await _paymentService.MarkPaidAsync(providerId);
    //    }
    //    else if (providerId is not null)
    //    {
    //        await _paymentService.MarkFailedAsync(providerId, status);
    //    }

    //    return Ok();
    //}

    //public record CreatePaymentRequest(string OrderId, decimal Amount, string ReturnUrl, CardDetails CardDetails);
}