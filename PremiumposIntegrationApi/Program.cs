using Microsoft.EntityFrameworkCore;
using PremiumposIntegrationApi;
using PremiumposIntegrationApi.Authentication;
using PremiumposIntegrationApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("PremiumposIntegrationDb"));

builder.Services.AddHttpClient("moyassar", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Moyassar:BaseUrl"] ?? "https://api.moyasar.com/");
});

builder.Services.AddHttpClient("redbox", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Redbox:BaseUrl"] ?? "https://api.redbox.example/");
});

builder.Services.AddScoped<MoyassarClient>();
builder.Services.AddScoped<RedboxClient>();
builder.Services.AddScoped<TenantSecretKeyValidator>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<DeliveryService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://fooddelivery.premium-pos.com"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services
    .AddAuthentication(SecretKeyAuthenticationHandler.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, SecretKeyAuthenticationHandler>(
        SecretKeyAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
