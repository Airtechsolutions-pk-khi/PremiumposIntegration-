using Dapper;


namespace PremiumposIntegrationApi.Services;

public sealed class ShipmentRepository
{
    private readonly DbConnectionFactory _db;

    public ShipmentRepository(DbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<int> CreateAsync(Shipment shipment, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO [dbo].[Shipments]
                (OrderId, ProviderShipmentId, TrackingUrl, Status, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES
                (@OrderId, @ProviderShipmentId, @TrackingUrl, @Status, @CreatedAt);";

        using var conn = _db.CreateConnection();
        var id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
        {
            shipment.OrderId,
            shipment.ProviderShipmentId,
            shipment.TrackingUrl,
            Status = (int)shipment.Status,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken: ct));

        return id;
    }

    public async Task<Shipment?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = @"SELECT * FROM [dbo].[Shipments] WHERE Id = @Id;";
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Shipment>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<Shipment?> GetByProviderShipmentIdAsync(string providerShipmentId, CancellationToken ct = default)
    {
        const string sql = @"SELECT * FROM [dbo].[Shipments] WHERE ProviderShipmentId = @ProviderShipmentId;";
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Shipment>(
            new CommandDefinition(sql, new { ProviderShipmentId = providerShipmentId }, cancellationToken: ct));
    }

    public async Task<Shipment?> GetLatestByOrderIdAsync(string orderId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT TOP 1 * FROM [dbo].[Shipments]
            WHERE OrderId = @OrderId
            ORDER BY CreatedAt DESC;";
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Shipment>(
            new CommandDefinition(sql, new { OrderId = orderId }, cancellationToken: ct));
    }

    public async Task UpdateStatusAsync(string providerShipmentId, ShipmentStatus status, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE [dbo].[Shipments]
            SET Status = @Status, UpdatedAt = GETUTCDATE()
            WHERE ProviderShipmentId = @ProviderShipmentId;";
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ProviderShipmentId = providerShipmentId,
            Status = (int)status
        }, cancellationToken: ct));
    }
}