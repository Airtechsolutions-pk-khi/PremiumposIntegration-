using System.Net.Http.Json;

namespace PremiumposIntegrationApi.Services;

public class PaymentService
{
    private readonly PaymentRepository _payments;
    private readonly MoyassarClient _moyassarClient;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpClient _foodCarrierClient;

    public PaymentService(
        PaymentRepository payments,
        MoyassarClient moyassarClient,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _payments = payments;
        _moyassarClient = moyassarClient;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;

        _foodCarrierClient = _httpClientFactory.CreateClient("foodCarrier");
        _foodCarrierClient.BaseAddress = new Uri(_configuration["FoodCarrier:BaseUrl"]);
        _foodCarrierClient.DefaultRequestHeaders.Add("X-Internal-Secret", _configuration["FoodCarrier:ApiKey"]);
    }

    public async Task<(string PaymentId, string RedirectUrl)> CreateInvoiceForOrderAsync(
        string orderId, decimal amountSar, string currency)
    {
        long amountHalalas = (long)(amountSar * 100);

        var invoice = await _moyassarClient.CreateInvoiceAsync(
            amountHalalas,
            currency,
            $"Order {orderId}",
            callbackUrl: $"{_configuration["Moyassar:BaseUrl"]}/api/payments/webhook",
            successUrl: $"{_configuration["Moyassar:FrontendBaseUrl"]}/payment/result?orderId={orderId}",
            backUrl: $"{_configuration["Moyassar:FrontendBaseUrl"]}/payment/result?orderId={orderId}",
            expiredAtUtc: DateTime.UtcNow.AddMinutes(30),
            metadata: new Dictionary<string, string> { ["order_id"] = orderId });

        var payment = new Payment
        {
            OrderId = orderId,
            ProviderInvoiceId = invoice.Id,
            Amount = amountSar,
            Status = PaymentStatus.Initiated,
            CreatedAt = DateTime.UtcNow
        };

        var newId = await _payments.CreateAsync(payment);
        payment.Id = newId;

        return (payment.Id.ToString(), invoice.Url);
    }

    public async Task SyncFromVerifiedInvoiceAsync(MoyasarInvoice invoice)
    {
        var payment = await _payments.GetByProviderInvoiceIdAsync(invoice.Id);
        if (payment == null) return;

        // Idempotency: already terminal, nothing to do.
        if (payment.Status == PaymentStatus.Paid || payment.Status == PaymentStatus.Failed)
            return;

        payment.Status = invoice.Status switch
        {
            "paid" => PaymentStatus.Paid,
            "failed" => PaymentStatus.Failed,
            "expired" => PaymentStatus.Expired,
            "canceled" => PaymentStatus.Cancelled,
            "refunded" => PaymentStatus.Refunded,
            _ => PaymentStatus.Pending
        };

        await _payments.UpdateStatusAsync(payment.Id, payment.Status);

        var orderId = invoice.Metadata != null && invoice.Metadata.TryGetValue("order_id", out var oid)
            ? oid : payment.OrderId;

        // Sync back to FoodCarrier — the actual order confirmation lives there.
        var content = JsonContent.Create(new
        {
            ProviderPaymentId = invoice.Id,
            Status = payment.Status.ToString().ToLower()
        });

        await _foodCarrierClient.PutAsync(
            $"orders/{orderId}/payment-status",
            content);
    }

    public async Task<Payment> GetLatestPaymentForOrderAsync(string orderId)
    {
        return await _payments.GetLatestByOrderIdAsync(orderId);
    }

    public async Task<bool> RefundPaymentAsync(string orderId, string? providerPaymentId, string reason)
    {
        var payment = await _payments.GetByOrderIdAndProviderIdAsync(orderId, providerPaymentId);

        if (payment == null) return false;

        // Call Moyasar refund API
        var refundResult = await _moyassarClient.RefundInvoiceAsync(payment.ProviderInvoiceId, reason);

        if (refundResult)
        {
            await _payments.UpdateStatusAsync(payment.Id, PaymentStatus.Refunding);

            // Notify FoodCarrier
            var content = JsonContent.Create(new
            {
                ProviderPaymentId = payment.ProviderInvoiceId,
                Status = "refunding"
            });

            await _foodCarrierClient.PutAsync(
                $"orders/{orderId}/payment-status",
                content);
        }

        return refundResult;
    }

    //public record PaymentCreationResult(Payment Payment, string? RedirectUrl, bool IsSuccess, string? ErrorMessage, string SerializedLog);

    //public async Task<PaymentCreationResult> CreatePaymentForOrderAsync(
    //    string orderId, 
    //    decimal amount, 
    //    string returnUrl, 
    //    string? tenantId,
    //    CardDetails cardDetails)
    //{
    //    try
    //    {
    //        var result = await _moyassarClient.CreatePaymentAsync(orderId, amount, returnUrl, tenantId, cardDetails);

    //        var payment = new Payment
    //        {
    //            OrderId = orderId,
    //            Amount = amount,
    //            Currency = "SAR",
    //            ProviderPaymentId = result.ProviderPaymentId,
    //            TenantId = tenantId,
    //            Status = result.IsSuccess ? PaymentStatus.Pending : PaymentStatus.Failed,
    //            Metadata = $"returnUrl:{returnUrl};tenantId:{tenantId}"
    //        };

    //        _dbContext.Payments.Add(payment);
    //        await _dbContext.SaveChangesAsync();

    //        _dbContext.PaymentLogs.Add(new PaymentLog
    //        {
    //            PaymentId = payment.Id,
    //            Operation = "create",
    //            Provider = "moyassar",
    //            Status = result.IsSuccess ? "success" : "failed",
    //            Message = result.IsSuccess ? "Payment initiation succeeded." : result.ErrorMessage ?? "Payment initiation failed.",
    //            SerializedLog = result.SerializedLog
    //        });

    //        await _dbContext.SaveChangesAsync();
    //        return new PaymentCreationResult(payment, result.RedirectUrl, result.IsSuccess, result.ErrorMessage, result.SerializedLog);
    //    }
    //    catch (Exception ex)
    //    {
    //        var failedPayment = new Payment
    //        {
    //            OrderId = orderId,
    //            Amount = amount,
    //            Currency = "SAR",
    //            TenantId = tenantId,
    //            Status = PaymentStatus.Failed,
    //            Metadata = $"returnUrl:{returnUrl};tenantId:{tenantId}"
    //        };

    //        _dbContext.Payments.Add(failedPayment);
    //        await _dbContext.SaveChangesAsync();

    //        var failureLog = JsonSerializer.Serialize(new
    //        {
    //            @event = "payment.create.exception",
    //            orderId,
    //            amount,
    //            returnUrl,
    //            error = ex.Message,
    //            timestamp = DateTime.UtcNow
    //        });

    //        _dbContext.PaymentLogs.Add(new PaymentLog
    //        {
    //            PaymentId = failedPayment.Id,
    //            Operation = "create",
    //            Provider = "moyassar",
    //            Status = "failed",
    //            Message = ex.Message,
    //            SerializedLog = failureLog
    //        });

    //        await _dbContext.SaveChangesAsync();
    //        return new PaymentCreationResult(failedPayment, null, false, ex.Message, failureLog);
    //    }
    //}

    //// MarkPaidAsync and MarkFailedAsync methods remain the same
    //public async Task<bool> MarkPaidAsync(string providerPaymentId)
    //{
    //    var payment = _dbContext.Payments.FirstOrDefault(p => p.ProviderPaymentId == providerPaymentId);
    //    if (payment is null)
    //    {
    //        return false;
    //    }

    //    payment.Status = PaymentStatus.Paid;
    //    await _dbContext.SaveChangesAsync();

    //    var logPayload = JsonSerializer.Serialize(new
    //    {
    //        @event = "payment.finalized",
    //        provider = "moyassar",
    //        providerPaymentId,
    //        orderId = payment.OrderId,
    //        status = "paid",
    //        timestamp = DateTime.UtcNow
    //    });

    //    _dbContext.PaymentLogs.Add(new PaymentLog
    //    {
    //        PaymentId = payment.Id,
    //        Operation = "webhook",
    //        Provider = "moyassar",
    //        Status = "success",
    //        Message = "Payment marked as paid from webhook.",
    //        SerializedLog = logPayload
    //    });

    //    await _dbContext.SaveChangesAsync();
    //    return true;
    //}

    //public async Task<bool> MarkFailedAsync(string providerPaymentId, string? reason)
    //{
    //    var payment = _dbContext.Payments.FirstOrDefault(p => p.ProviderPaymentId == providerPaymentId);
    //    if (payment is null)
    //    {
    //        return false;
    //    }

    //    payment.Status = PaymentStatus.Failed;
    //    await _dbContext.SaveChangesAsync();

    //    var logPayload = JsonSerializer.Serialize(new
    //    {
    //        @event = "payment.failed",
    //        provider = "moyassar",
    //        providerPaymentId,
    //        reason,
    //        orderId = payment.OrderId,
    //        timestamp = DateTime.UtcNow
    //    });

    //    _dbContext.PaymentLogs.Add(new PaymentLog
    //    {
    //        PaymentId = payment.Id,
    //        Operation = "webhook",
    //        Provider = "moyassar",
    //        Status = "failed",
    //        Message = reason ?? "Payment marked as failed from webhook.",
    //        SerializedLog = logPayload
    //    });

    //    await _dbContext.SaveChangesAsync();
    //    return true;
    //}
}