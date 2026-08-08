namespace PremiumposIntegrationApi.Services;

public class DeliveryService
{
    private readonly AppDbContext _dbContext;
    private readonly RedboxClient _redboxClient;

    public DeliveryService(AppDbContext dbContext, RedboxClient redboxClient)
    {
        _dbContext = dbContext;
        _redboxClient = redboxClient;
    }

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

        _dbContext.Shipments.Add(shipment);
        await _dbContext.SaveChangesAsync();

        return shipment;
    }

    public async Task<bool> MarkDeliveredAsync(string providerShipmentId)
    {
        var shipment = _dbContext.Shipments.FirstOrDefault(s => s.ProviderShipmentId == providerShipmentId);
        if (shipment is null)
        {
            return false;
        }

        shipment.Status = ShipmentStatus.Delivered;
        await _dbContext.SaveChangesAsync();
        return true;
    }
}
