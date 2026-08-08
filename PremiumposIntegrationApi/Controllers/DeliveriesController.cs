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

    public DeliveriesController(DeliveryService deliveryService, RedboxClient redboxClient)
    {
        _deliveryService = deliveryService;
        _redboxClient = redboxClient;
    }

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

    public record CreateShipmentRequest(string OrderId, string Recipient, string Address, decimal WeightKg);
}
