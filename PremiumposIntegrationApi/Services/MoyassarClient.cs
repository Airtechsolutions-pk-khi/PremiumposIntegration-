using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PremiumposIntegrationApi.Services;

public class MoyassarClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _http;
    private readonly string _secretKey;
    private readonly string _webhookSecret;

    public MoyassarClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;

        _secretKey = configuration["Moyasar:SecretKey"] ?? configuration["Moyassar:ApiKey"] ??
                     Environment.GetEnvironmentVariable("MOYASSAR_API_KEY") ?? "demo-key";
        _webhookSecret = configuration["Moyasar:WebhookSecret"] ??
                         Environment.GetEnvironmentVariable("MOYASSAR_WEBHOOK_SECRET") ?? "demo-webhook-secret";

        _http = _httpClientFactory.CreateClient("moyassar");
        var authBytes = Encoding.UTF8.GetBytes($"{_secretKey}:");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        _http.BaseAddress = new Uri("https://api.moyasar.com/v1/");
    }

    // ==================== CREATE INVOICE ====================

    public async Task<MoyasarInvoice> CreateInvoiceAsync(
        long amountInHalalas, string currency, string description,
        string callbackUrl, string successUrl, string backUrl,
        DateTime expiredAtUtc, Dictionary<string, string> metadata)
    {
        var payload = new CreateInvoiceRequest(
            Amount: amountInHalalas,
            Currency: currency,
            Description: description,
            CallbackUrl: callbackUrl,
            SuccessUrl: successUrl,
            BackUrl: backUrl,
            ExpiredAt: expiredAtUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            Metadata: metadata);

        var resp = await _http.PostAsJsonAsync("invoices", payload);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new MoyasarApiException($"Invoice creation failed: {resp.StatusCode} - {body}");

        return JsonSerializer.Deserialize<MoyasarInvoice>(body) ??
               throw new MoyasarApiException("Failed to deserialize invoice response");
    }

    // ==================== FETCH INVOICE ====================

    public async Task<MoyasarInvoice> FetchInvoiceAsync(string invoiceId)
    {
        var resp = await _http.GetAsync($"invoices/{invoiceId}");
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new MoyasarApiException($"Fetch invoice failed: {resp.StatusCode} - {body}");

        return JsonSerializer.Deserialize<MoyasarInvoice>(body) ??
               throw new MoyasarApiException("Failed to deserialize invoice response");
    }

    // ==================== VERIFY WEBHOOK ====================


    public bool VerifyWebhookSecret(JsonElement root)
    {
        if (!root.TryGetProperty("secret_token", out var tokenProp))
            return false;

        var received = tokenProp.GetString() ?? "";
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(received),
            Encoding.UTF8.GetBytes(_webhookSecret));
    }

    // ==================== REFUND INVOICE ====================

    public async Task<bool> RefundInvoiceAsync(string invoiceId, string reason)
    {
        var payload = new RefundRequest(Reason: reason);
        var resp = await _http.PostAsJsonAsync($"invoices/{invoiceId}/refund", payload);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new MoyasarApiException($"Refund failed: {resp.StatusCode} - {body}");

        return true;
    }
}

// ==================== REQUEST DTOs (snake_case) ====================

public record CreateInvoiceRequest(
    [property: JsonPropertyName("amount")] long Amount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("callback_url")] string CallbackUrl,
    [property: JsonPropertyName("success_url")] string SuccessUrl,
    [property: JsonPropertyName("back_url")] string BackUrl,
    [property: JsonPropertyName("expired_at")] string ExpiredAt,
    [property: JsonPropertyName("metadata")] Dictionary<string, string> Metadata);

public record RefundRequest(
    [property: JsonPropertyName("reason")] string Reason);

// ==================== RESPONSE DTO (snake_case) ====================

public class MoyasarInvoice
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";   // "initiated" | "paid" | "failed" | "expired" | "canceled" | "refunded"

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("logo_url")]
    public string? LogoUrl { get; set; }

    [JsonPropertyName("amount_format")]
    public string? AmountFormat { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";      // <-- redirect URL for frontend

    [JsonPropertyName("callback_url")]
    public string? CallbackUrl { get; set; }

    [JsonPropertyName("expired_at")]
    public DateTime? ExpiredAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("back_url")]
    public string? BackUrl { get; set; }

    [JsonPropertyName("success_url")]
    public string? SuccessUrl { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    [JsonPropertyName("payments")]
    public List<JsonElement>? Payments { get; set; }
}

public class MoyasarApiException : Exception
{
    public MoyasarApiException(string message) : base(message) { }
}