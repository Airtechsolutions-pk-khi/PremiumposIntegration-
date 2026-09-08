using System.Text.Json;


namespace PremiumposIntegrationApi.Services;

public class PaymentService
{
    private readonly AppDbContext _dbContext;
    private readonly MoyassarClient _moyassarClient;

    public PaymentService(AppDbContext dbContext, MoyassarClient moyassarClient)
    {
        _dbContext = dbContext;
        _moyassarClient = moyassarClient;
    }

    public record PaymentCreationResult(Payment Payment, string? RedirectUrl, bool IsSuccess, string? ErrorMessage, string SerializedLog);

    public async Task<PaymentCreationResult> CreatePaymentForOrderAsync(
        string orderId, 
        decimal amount, 
        string returnUrl, 
        string? tenantId,
        CardDetails cardDetails)
    {
        try
        {
            var result = await _moyassarClient.CreatePaymentAsync(orderId, amount, returnUrl, tenantId, cardDetails);

            var payment = new Payment
            {
                OrderId = orderId,
                Amount = amount,
                Currency = "SAR",
                ProviderPaymentId = result.ProviderPaymentId,
                TenantId = tenantId,
                Status = result.IsSuccess ? PaymentStatus.Pending : PaymentStatus.Failed,
                Metadata = $"returnUrl:{returnUrl};tenantId:{tenantId}"
            };

            _dbContext.Payments.Add(payment);
            await _dbContext.SaveChangesAsync();

            _dbContext.PaymentLogs.Add(new PaymentLog
            {
                PaymentId = payment.Id,
                Operation = "create",
                Provider = "moyassar",
                Status = result.IsSuccess ? "success" : "failed",
                Message = result.IsSuccess ? "Payment initiation succeeded." : result.ErrorMessage ?? "Payment initiation failed.",
                SerializedLog = result.SerializedLog
            });

            await _dbContext.SaveChangesAsync();
            return new PaymentCreationResult(payment, result.RedirectUrl, result.IsSuccess, result.ErrorMessage, result.SerializedLog);
        }
        catch (Exception ex)
        {
            var failedPayment = new Payment
            {
                OrderId = orderId,
                Amount = amount,
                Currency = "SAR",
                TenantId = tenantId,
                Status = PaymentStatus.Failed,
                Metadata = $"returnUrl:{returnUrl};tenantId:{tenantId}"
            };

            _dbContext.Payments.Add(failedPayment);
            await _dbContext.SaveChangesAsync();

            var failureLog = JsonSerializer.Serialize(new
            {
                @event = "payment.create.exception",
                orderId,
                amount,
                returnUrl,
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });

            _dbContext.PaymentLogs.Add(new PaymentLog
            {
                PaymentId = failedPayment.Id,
                Operation = "create",
                Provider = "moyassar",
                Status = "failed",
                Message = ex.Message,
                SerializedLog = failureLog
            });

            await _dbContext.SaveChangesAsync();
            return new PaymentCreationResult(failedPayment, null, false, ex.Message, failureLog);
        }
    }

    // MarkPaidAsync and MarkFailedAsync methods remain the same
    public async Task<bool> MarkPaidAsync(string providerPaymentId)
    {
        var payment = _dbContext.Payments.FirstOrDefault(p => p.ProviderPaymentId == providerPaymentId);
        if (payment is null)
        {
            return false;
        }

        payment.Status = PaymentStatus.Paid;
        await _dbContext.SaveChangesAsync();

        var logPayload = JsonSerializer.Serialize(new
        {
            @event = "payment.finalized",
            provider = "moyassar",
            providerPaymentId,
            orderId = payment.OrderId,
            status = "paid",
            timestamp = DateTime.UtcNow
        });

        _dbContext.PaymentLogs.Add(new PaymentLog
        {
            PaymentId = payment.Id,
            Operation = "webhook",
            Provider = "moyassar",
            Status = "success",
            Message = "Payment marked as paid from webhook.",
            SerializedLog = logPayload
        });

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkFailedAsync(string providerPaymentId, string? reason)
    {
        var payment = _dbContext.Payments.FirstOrDefault(p => p.ProviderPaymentId == providerPaymentId);
        if (payment is null)
        {
            return false;
        }

        payment.Status = PaymentStatus.Failed;
        await _dbContext.SaveChangesAsync();

        var logPayload = JsonSerializer.Serialize(new
        {
            @event = "payment.failed",
            provider = "moyassar",
            providerPaymentId,
            reason,
            orderId = payment.OrderId,
            timestamp = DateTime.UtcNow
        });

        _dbContext.PaymentLogs.Add(new PaymentLog
        {
            PaymentId = payment.Id,
            Operation = "webhook",
            Provider = "moyassar",
            Status = "failed",
            Message = reason ?? "Payment marked as failed from webhook.",
            SerializedLog = logPayload
        });

        await _dbContext.SaveChangesAsync();
        return true;
    }
}