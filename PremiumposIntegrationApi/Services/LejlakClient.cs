using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PremiumposIntegrationApi.Services;

public class LeajlakClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LeajlakClient> _logger;
    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public LeajlakClient(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<LeajlakClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("leajlak");
        _configuration = configuration;
        _logger = logger;
    }

    private async Task<string> GetBearerTokenAsync()
    {
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
        {
            return _cachedToken;
        }

        var credentials = new
        {
            email = _configuration["Leajlak:Username"],
            password = _configuration["Leajlak:Password"]
        };

        var response = await _httpClient.PostAsJsonAsync("/token", credentials);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        _cachedToken = content.GetProperty("token").GetString() ??
                       content.GetProperty("access_token").GetString();
        _tokenExpiry = DateTime.UtcNow.AddHours(23); // Assuming token valid for 24 hours

        return _cachedToken ?? throw new InvalidOperationException("Failed to retrieve token");
    }

    private async Task SetAuthorizationHeaderAsync()
    {
        var token = await GetBearerTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<CreateOrderResponse> CreateOrderAsync(CreateOrderRequest request)
    {
        await SetAuthorizationHeaderAsync();

        var response = await _httpClient.PostAsJsonAsync("/orders", request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Leajlak CreateOrder failed. Status: {StatusCode}, Error: {Error}",
                response.StatusCode, error);
            throw new HttpRequestException($"Leajlak CreateOrder failed: {error}");
        }

        return await response.Content.ReadFromJsonAsync<CreateOrderResponse>() ??
               throw new InvalidOperationException("Failed to deserialize create order response");
    }

    public async Task<OrderDetailsResponse> GetOrderDetailsAsync(int orderId)
    {
        await SetAuthorizationHeaderAsync();

        var response = await _httpClient.GetAsync($"/api/partner-v2/orders/{orderId}");

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Leajlak GetOrderDetails failed. Status: {StatusCode}, Error: {Error}",
                response.StatusCode, error);
            throw new HttpRequestException($"Leajlak GetOrderDetails failed: {error}");
        }

        return await response.Content.ReadFromJsonAsync<OrderDetailsResponse>() ??
               throw new InvalidOperationException("Failed to deserialize order details response");
    }

    public async Task CancelOrderAsync(int orderId)
    {
        await SetAuthorizationHeaderAsync();

        var response = await _httpClient.DeleteAsync($"/orders/{orderId}");

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Leajlak CancelOrder failed. Status: {StatusCode}, Error: {Error}",
                response.StatusCode, error);
            throw new HttpRequestException($"Leajlak CancelOrder failed: {error}");
        }
    }
}

// DTOs for Leajlak API
public record CreateOrderRequest(
    string Id,
    string ShopId,
    DeliveryDetails DeliveryDetails,
    OrderDetails Order);

public record DeliveryDetails(
    string Name,
    string Phone,
    string Address,
    Coordinate Coordinate);

public record Coordinate(
    double Latitude,
    double Longitude);

public record OrderDetails(
    int PaymentType,
    decimal Total);

public record CreateOrderResponse(
    int Id,
    string Status,
    string TrackingNumber,
    string Message);

public record OrderDetailsResponse(
    int Id,
    string Status,
    string TrackingNumber,
    string CustomerName,
    string CustomerPhone,
    string DeliveryAddress,
    string DriverName,
    string DriverPhone,
    DateTime? CreatedAt,
    DateTime? UpdatedAt,
    decimal Total,
    int PaymentType);