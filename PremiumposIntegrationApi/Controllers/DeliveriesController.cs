using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PremiumposIntegrationApi.Services;

namespace PremiumposIntegrationApi.Controllers;

[ApiController]
[Route("api/deliveries")]
[Authorize]
public class DeliveriesController : ControllerBase
{
    private readonly DeliveryService _deliveryService;
    private readonly RedboxClient _redboxClient;
    private readonly ILogger<DeliveriesController> _logger;

    public DeliveriesController(
        DeliveryService deliveryService,
        RedboxClient redboxClient,
        ILogger<DeliveriesController> logger)
    {
        _deliveryService = deliveryService;
        _redboxClient = redboxClient;
        _logger = logger;
    }

    // Existing Redbox endpoints remain unchanged
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateShipmentRequest request)
    {
        var shipment = await _deliveryService.CreateShipmentAsync(request.OrderId, request.Recipient, request.Address, request.WeightKg);
        return Ok(new { shipmentId = shipment.Id, providerShipmentId = shipment.ProviderShipmentId, trackingUrl = shipment.TrackingUrl });
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        var body = ms.ToArray();
        var signatureHeader = Request.Headers["X-Redbox-Signature"].ToString();

        if (!_redboxClient.VerifyWebhook(body, signatureHeader))
        {
            return Unauthorized();
        }

        using var json = JsonDocument.Parse(body);
        var providerShipmentId = json.RootElement.GetProperty("id").GetString();
        var status = json.RootElement.GetProperty("status").GetString();

        if (status == "delivered" && providerShipmentId is not null)
        {
            await _deliveryService.MarkDeliveredAsync(providerShipmentId);
        }

        return Ok();
    }

    // New Leajlak endpoints
    [HttpPost("leajlak/create")]
    public async Task<IActionResult> CreateLeajlakOrder([FromBody] CreateLeajlakOrderRequest request)
    {
        try
        {
            var shipment = await _deliveryService.CreateLeajlakOrderAsync(
                request.OrderId,
                request.ShopId,
                request.CustomerName,
                request.CustomerPhone,
                request.DeliveryAddress,
                request.Latitude,
                request.Longitude,
                request.PaymentType,
                request.Total);

            return Ok(new
            {
                shipmentId = shipment.Id,
                providerShipmentId = shipment.ProviderShipmentId,
                trackingNumber = shipment.TrackingUrl,
                status = shipment.Status.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Leajlak order for order ID {OrderId}", request.OrderId);
            return StatusCode(500, new { message = "Failed to create Leajlak order", error = ex.Message });
        }
    }


    [HttpGet("leajlak/{providerShipmentId}/status")]
    public async Task<IActionResult> GetLeajlakOrderStatus(string providerShipmentId)
    {
        try
        {
            var status = await _deliveryService.GetLeajlakOrderStatusAsync(providerShipmentId);
            return Ok(status);
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "Invalid Leajlak order ID format" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Leajlak order status for provider shipment ID {ProviderShipmentId}", providerShipmentId);
            return StatusCode(500, new { message = "Failed to retrieve Leajlak order status", error = ex.Message });
        }
    }

    [HttpDelete("leajlak/{providerShipmentId}")]
    public async Task<IActionResult> CancelLeajlakOrder(string providerShipmentId)
    {
        try
        {
            var cancelled = await _deliveryService.CancelLeajlakOrderAsync(providerShipmentId);

            if (!cancelled)
            {
                return NotFound(new { message = "Shipment not found" });
            }

            return Ok(new { message = "Order cancelled successfully", providerShipmentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel Leajlak order for provider shipment ID {ProviderShipmentId}", providerShipmentId);
            return StatusCode(500, new { message = "Failed to cancel Leajlak order", error = ex.Message });
        }
    }

    public record CreateShipmentRequest(string OrderId, string Recipient, string Address, decimal WeightKg);

    public record CreateLeajlakOrderRequest(
        string OrderId,
        string ShopId,
        string CustomerName,
        string CustomerPhone,
        string DeliveryAddress,
        double Latitude,
        double Longitude,
        int PaymentType,
        decimal Total);
}