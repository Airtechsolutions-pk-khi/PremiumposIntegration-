using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace PremiumposIntegrationApi.Services;

public class RedboxClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public RedboxClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public record CreateShipmentResult(string ProviderShipmentId, string TrackingUrl);

    public async Task<CreateShipmentResult> CreateShipmentAsync(string orderId, string recipient, string address, decimal weightKg)
    {
        var client = _httpClientFactory.CreateClient("redbox");
        var apiKey = _configuration["Redbox:ApiKey"] ?? "demo-key";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new { orderId, recipient, address, weight = weightKg };
        var response = await client.PostAsJsonAsync("/v1/shipments", payload);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>();
        var id = json?.GetValueOrDefault("id")?.ToString() ?? Guid.NewGuid().ToString();
        var tracking = json?.GetValueOrDefault("tracking_url")?.ToString();

        return new CreateShipmentResult(id, tracking ?? string.Empty);
    }

    public bool VerifyWebhook(byte[] payloadBody, string signatureHeader)
    {
        var secret = _configuration["Redbox:WebhookSecret"] ?? "demo-webhook-secret";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexString(hmac.ComputeHash(payloadBody)).ToLowerInvariant();

        return signatureHeader.Equals(hash, StringComparison.OrdinalIgnoreCase)
            || signatureHeader.Equals($"sha256={hash}", StringComparison.OrdinalIgnoreCase);
    }
}
