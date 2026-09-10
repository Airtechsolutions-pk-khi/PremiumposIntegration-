using Dapper;

namespace PremiumposIntegrationApi.Services;

public sealed class PaymentRepository
{
    private readonly DbConnectionFactory _db;

    public PaymentRepository(DbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<int> CreateAsync(Payment payment, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO [dbo].[Payments]
                (OrderId, ProviderInvoiceId, Amount, Status, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES
                (@OrderId, @ProviderInvoiceId, @Amount, @Status, @CreatedAt);";

        using var conn = _db.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
        {
            payment.OrderId,
            payment.ProviderInvoiceId,
            payment.Amount,
            Status = (int)payment.Status,
            payment.CreatedAt
        }, cancellationToken: ct));
    }

    public async Task<Payment?> GetByProviderInvoiceIdAsync(string providerInvoiceId, CancellationToken ct = default)
    {
        const string sql = @"SELECT * FROM [dbo].[Payments] WHERE ProviderInvoiceId = @ProviderInvoiceId;";
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Payment>(
            new CommandDefinition(sql, new { ProviderInvoiceId = providerInvoiceId }, cancellationToken: ct));
    }

    public async Task<Payment?> GetLatestByOrderIdAsync(string orderId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT TOP 1 * FROM [dbo].[Payments]
            WHERE OrderId = @OrderId
            ORDER BY CreatedAt DESC;";
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Payment>(
            new CommandDefinition(sql, new { OrderId = orderId }, cancellationToken: ct));
    }

    public async Task<Payment?> GetByOrderIdAndProviderIdAsync(string orderId, string? providerInvoiceId, CancellationToken ct = default)
    {
        // Matches the exact filter in RefundPaymentAsync
        const string sql = @"
            SELECT TOP 1 * FROM [dbo].[Payments]
            WHERE OrderId = @OrderId
              AND (@ProviderInvoiceId IS NULL OR ProviderInvoiceId = @ProviderInvoiceId);";
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Payment>(
            new CommandDefinition(sql, new { OrderId = orderId, ProviderInvoiceId = providerInvoiceId }, cancellationToken: ct));
    }

    public async Task UpdateStatusAsync(int paymentId, PaymentStatus status, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE [dbo].[Payments]
            SET Status = @Status, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id;";
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = paymentId,
            Status = (int)status
        }, cancellationToken: ct));
    }

    public async Task AddLogAsync(PaymentLog log, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO [dbo].[PaymentLogs]
                (PaymentId, Operation, Provider, Status, Message, SerializedLog, Timestamp)
            VALUES
                (@PaymentId, @Operation, @Provider, @Status, @Message, @SerializedLog, @Timestamp);";
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            log.PaymentId,
            log.Operation,
            log.Provider,
            log.Status,
            log.Message,
            log.SerializedLog,
            Timestamp = DateTime.UtcNow
        }, cancellationToken: ct));
    }
}