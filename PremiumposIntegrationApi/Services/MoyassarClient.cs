using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PremiumposIntegrationApi.Services;

public class MoyassarClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public MoyassarClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public record CreatePaymentResult(bool IsSuccess, string? ProviderPaymentId, string? RedirectUrl, string? ErrorMessage, string SerializedLog);

    public async Task<CreatePaymentResult> CreatePaymentAsync(
        string orderId,
        decimal amount,
        string returnUrl,
        string? tenantId,
        CardDetails cardDetails)
    {
        var client = _httpClientFactory.CreateClient("moyassar");
        var apiKey = _configuration["Moyassar:ApiKey"] ?? Environment.GetEnvironmentVariable("MOYASSAR_API_KEY") ?? "demo-key";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey)));

        // Use card details from the client request instead of configuration
        var payload = new
        {
            amount = (int)(amount * 100),
            currency = "SAR",
            description = $"Payment for order {orderId}",
            callback_url = returnUrl,
            metadata = new { orderId, tenantId },
            source = new
            {
                type = "creditcard",
                name = cardDetails.Name,
                number = cardDetails.Number,
                cvc = cardDetails.Cvc,
                month = cardDetails.Month,
                year = cardDetails.Year
            }
        };

        var requestLog = new
        {
            operation = "create_payment",
            provider = "moyassar",
            endpoint = "/v1/payments",
            tenantId,
            payload,
            timestamp = DateTime.UtcNow
        };

        try
        {
            var response = await client.PostAsJsonAsync("/v1/payments", payload);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var failureLog = new
                {
                    requestLog,
                    statusCode = (int)response.StatusCode,
                    responseBody,
                    result = "failed",
                    errorMessage = "Moyassar rejected the payment creation request."
                };

                return new CreatePaymentResult(false, null, null, "Payment creation failed", JsonSerializer.Serialize(failureLog));
            }

            var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseBody);
            var providerId = GetStringValue(json, "id") ?? Guid.NewGuid().ToString();
            var redirect = GetStringValue(json, "checkout_url") ?? returnUrl;

            var successLog = new
            {
                requestLog,
                statusCode = (int)response.StatusCode,
                responseBody,
                result = "success",
                providerPaymentId = providerId,
                redirectUrl = redirect
            };

            return new CreatePaymentResult(true, providerId, redirect, null, JsonSerializer.Serialize(successLog));
        }
        catch (Exception ex)
        {
            var errorLog = new
            {
                requestLog,
                result = "failed",
                errorMessage = ex.Message
            };

            return new CreatePaymentResult(false, null, null, "Payment creation failed", JsonSerializer.Serialize(errorLog));
        }
    }

    // GetPaymentStatusAsync and VerifyWebhook methods remain the same
    public async Task<string?> GetPaymentStatusAsync(string providerPaymentId)
    {
        var client = _httpClientFactory.CreateClient("moyassar");
        var apiKey = _configuration["Moyassar:ApiKey"] ?? Environment.GetEnvironmentVariable("MOYASSAR_API_KEY") ?? "demo-key";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey)));

        var response = await client.GetAsync($"/v1/payments/{providerPaymentId}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>();
        return json?.GetValueOrDefault("status")?.ToString();
    }

    public bool VerifyWebhook(byte[] payloadBody, string signatureHeader)
    {
        var secret = _configuration["Moyassar:WebhookSecret"] ?? "demo-webhook-secret";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexString(hmac.ComputeHash(payloadBody)).ToLowerInvariant();

        return signatureHeader.Equals(hash, StringComparison.OrdinalIgnoreCase)
            || signatureHeader.Equals($"sha256={hash}", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetStringValue(Dictionary<string, JsonElement>? json, string key)
    {
        if (json is null || !json.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }
}