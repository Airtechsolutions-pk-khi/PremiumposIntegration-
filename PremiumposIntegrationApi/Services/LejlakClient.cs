using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;


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
        //hardcode for now
        return "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.eyJhdWQiOiIxMCIsImp0aSI6IjhlOTJjM2Y5YzY3NjAwNTc2ZmE0OWI3NzhlZjhhYmNlZDgxYzYxYjBlZDBmMDY0MGE3NTU4NGY2ZjBjODgwMWU4N2FjZWIyYTkxMGUwYmJjIiwiaWF0IjoxNzg4ODE5MzI5LjY1MjQxNywibmJmIjoxNzg4ODE5MzI5LjY1MjQyLCJleHAiOjQ5NDQ0OTI5MjkuNjQ2Njk4LCJzdWIiOiI1MzEiLCJzY29wZXMiOltdfQ.K-DHV2TwismCgPIdQP4LQBkfVwD2wOnx74A-p9yWfLN1X-goIAEjT4KdzV6X3FsvVzyLqrgsr5Tu1W5Td29VW4GYY1NEKU-cXkkanYIDD2AHl2gWfPdBMM8Ex4y7mIfskcEkPAr7xNjwX2VwvV1VZVCXk1VQMVf2ciCxMv4wXxbAUALVXe2WMdSCaaltZ4eEjW9XX916xkIuMk7xdARhTOsWxOQTN_FJS3jCiU2oijwO2N_R63D7c2fdxb8H_44ZVzJlXAVXcHCzj501hpR4dOf0E_tsVVqGf4zHuwOJxGi7OtSRsA5E7upG_wo9t9Y_Ub8ui7wi20gENpbkP17Zg3G66-Iof981zsRixN2TdKq-TcJbryUJBwXiM560xDGKbLQ9HTORRxekXIdlOG1fHrM8VB1b76hI_2vMBnxEqnfhwJzPpWeVYOW3C_aBaPhzVsnKf7C3o7LxMkaVUVaIKo7vJ_o3VRIxuzwW3MLoH9u5dV8jwS4izgvmSX-Bd50JeylwSuRMu75Nl7YwnV6OrnJzqvTtNpGQuK3E-brPk033EmIKokQexmk9Q7JCfkrzo1tolD32W3S3rnM5JWjnmD0N33z7km4cL_0Kgwz5bHeshtYJcLEvPmYU_FRQJsEQZYcENLnA-N8SWOmXjj2P8vqnzdxwjCjo7LSNw4iHMng";
        //if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
        //{
        //    return _cachedToken;
        //}

        //var credentials = new
        //{
        //    email = _configuration["Leajlak:Username"],
        //    password = _configuration["Leajlak:Password"]
        //};

        //var response = await _httpClient.PostAsJsonAsync("/token", credentials);
        //response.EnsureSuccessStatusCode();

        //var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        //_cachedToken = content.GetProperty("token").GetString() ??
        //               content.GetProperty("access_token").GetString();
        //_tokenExpiry = DateTime.UtcNow.AddHours(23); // Assuming token valid for 24 hours

        //return _cachedToken ?? throw new InvalidOperationException("Failed to retrieve token");
    }

    private async Task SetAuthorizationHeaderAsync()
    {
        var token = await GetBearerTokenAsync();

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<CreateOrderResponse> CreateOrderAsync(CreateOrderRequest request)
    {
        await SetAuthorizationHeaderAsync();

        var response = await _httpClient.PostAsJsonAsync("/api/partner-v2/orders", request);

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


public record CreateOrderRequest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("shop_id")] string ShopId,
    [property: JsonPropertyName("delivery_details")] DeliveryDetails DeliveryDetails,
    [property: JsonPropertyName("order")] OrderDetails Order);

public record DeliveryDetails(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("phone")] string Phone,
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("coordinate")] Coordinate Coordinate);

public record Coordinate(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude);

public record OrderDetails(
    [property: JsonPropertyName("payment_type")] int PaymentType,
    [property: JsonPropertyName("total")] decimal Total);


public record CreateOrderResponse(
    [property: JsonPropertyName("dsp_order_id")] string DspOrderId,
    [property: JsonPropertyName("status")] string Status);

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