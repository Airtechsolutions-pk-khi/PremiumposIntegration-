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

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreatePaymentRequest request)
    {
        try
        {
            var tenantId ="Test-Tenantid";// User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return Unauthorized(new { success = false, status = "failed", message = "Unauthorized request. Send a valid X-Secret-Key header." });
            }

            var result = await _paymentService.CreatePaymentForOrderAsync(request.OrderId, request.Amount, request.ReturnUrl, tenantId);
            var response = new
            {
                success = result.IsSuccess,
                status = result.IsSuccess ? "pending" : "failed",
                message = result.IsSuccess ? "Payment initiated successfully." : result.ErrorMessage ?? "Payment initiation failed.",
                paymentId = result.Payment.Id,
                providerPaymentId = result.Payment.ProviderPaymentId,
                redirectUrl = result.RedirectUrl,
                serialLog = result.SerializedLog
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            var fallback = new
            {
                success = false,
                status = "failed",
                message = "Payment initiation could not be completed.",
                serialLog = JsonSerializer.Serialize(new { @event = "payment.create.exception", error = ex.Message, timestamp = DateTime.UtcNow })
            };

            return StatusCode(500, fallback);
        }
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        var body = ms.ToArray();
        var signatureHeader = Request.Headers["X-Moyassar-Signature"].ToString();

        if (!_moyassarClient.VerifyWebhook(body, signatureHeader))
        {
            return Unauthorized();
        }

        using var json = JsonDocument.Parse(body);
        var providerId = json.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        var status = json.RootElement.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;

        if (providerId is not null && status == "paid")
        {
            await _paymentService.MarkPaidAsync(providerId);
        }
        else if (providerId is not null)
        {
            await _paymentService.MarkFailedAsync(providerId, status);
        }

        return Ok();
    }

    public record CreatePaymentRequest(string OrderId, decimal Amount, string ReturnUrl);
}
