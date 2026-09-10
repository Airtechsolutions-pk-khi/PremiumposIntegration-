using Microsoft.EntityFrameworkCore;

namespace PremiumposIntegrationApi.Services;

public class DeliveryService
{
    private readonly AppDbContext _dbContext;
    private readonly RedboxClient _redboxClient;
    private readonly LeajlakClient _leajlakClient;
    private readonly ILogger<DeliveryService> _logger;
    private readonly ShipmentRepository _shipments;


    public DeliveryService(
        AppDbContext dbContext,
        RedboxClient redboxClient,
        LeajlakClient leajlakClient,
        ILogger<DeliveryService> logger,
        ShipmentRepository shipments)
    {
        _dbContext = dbContext;
        _redboxClient = redboxClient;
        _leajlakClient = leajlakClient;
        _logger = logger;
        _shipments = shipments;
    }

    // Existing Redbox methods remain unchanged
    public async Task<Shipment> CreateShipmentAsync(string orderId, string recipient, string address, decimal weightKg)
    {
        var result = await _redboxClient.CreateShipmentAsync(orderId, recipient, address, weightKg);

        var shipment = new Shipment
        {
            OrderId = orderId,
            ProviderShipmentId = result.ProviderShipmentId,
            TrackingUrl = result.TrackingUrl,
            Status = ShipmentStatus.Booked
        };

        var newId = await _shipments.CreateAsync(shipment);
        shipment.Id = newId;

        return shipment;
    }

    public async Task MarkDeliveredAsync(string providerShipmentId)
    {
        await _shipments.UpdateStatusAsync(providerShipmentId, ShipmentStatus.Delivered);
    }

    // New Leajlak methods
    public async Task<Shipment> CreateLeajlakOrderAsync(
       string orderId, string shopId,
       string customerName, string customerPhone, string deliveryAddress,
       double latitude, double longitude,
       int paymentType, decimal total)
    {
        var leajlakRequest = new CreateOrderRequest(
            Id: orderId,
            ShopId: shopId,
            DeliveryDetails: new DeliveryDetails(
                Name: customerName,
                Phone: customerPhone,
                Address: deliveryAddress,
                Coordinate: new Coordinate(latitude, longitude)),
            Order: new OrderDetails(
                PaymentType: paymentType,
                Total: total));

        var result = await _leajlakClient.CreateOrderAsync(leajlakRequest);

        var shipment = new Shipment
        {
            OrderId = orderId,
            ProviderShipmentId = result.DspOrderId,
            TrackingUrl = null,
            Status = ShipmentStatus.Booked,
            CreatedAt = DateTime.UtcNow
        };

  
        var newId = await _shipments.CreateAsync(shipment);
        shipment.Id = newId;

        return shipment;
    }


    public async Task<LeajlakOrderStatus> GetLeajlakOrderStatusAsync(string providerShipmentId)
    {
        if (!int.TryParse(providerShipmentId, out int leajlakOrderId))
            throw new ArgumentException("Invalid Leajlak order ID");

        var orderDetails = await _leajlakClient.GetOrderDetailsAsync(leajlakOrderId);

        return new LeajlakOrderStatus(
            OrderId: orderDetails.Id,
            Status: orderDetails.Status,
            TrackingNumber: orderDetails.TrackingNumber,
            DriverName: orderDetails.DriverName,
            DriverPhone: orderDetails.DriverPhone,
            UpdatedAt: orderDetails.UpdatedAt);
    }

    public async Task<bool> CancelLeajlakOrderAsync(string providerShipmentId)
    {
        if (!int.TryParse(providerShipmentId, out int leajlakOrderId))
            return false;

        await _leajlakClient.CancelOrderAsync(leajlakOrderId);

        var shipment = await _shipments.GetByProviderShipmentIdAsync(providerShipmentId);
        if (shipment is null) return false;

        await _shipments.UpdateStatusAsync(providerShipmentId, ShipmentStatus.Cancelled);
        return true;
    }

}

public record LeajlakOrderStatus(
    int OrderId,
    string Status,
    string TrackingNumber,
    string? DriverName,
    string? DriverPhone,
    DateTime? UpdatedAt);