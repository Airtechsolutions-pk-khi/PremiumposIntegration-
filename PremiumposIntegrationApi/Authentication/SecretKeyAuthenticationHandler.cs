using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PremiumposIntegrationApi.Services;

namespace PremiumposIntegrationApi.Authentication;

public sealed class SecretKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "SecretKey";
    private readonly TenantSecretKeyValidator _tenantSecretKeyValidator;

    public SecretKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TenantSecretKeyValidator tenantSecretKeyValidator)
        : base(options, logger, encoder)
    {
        _tenantSecretKeyValidator = tenantSecretKeyValidator;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var secretKey = Request.Headers["X-Secret-Key"].FirstOrDefault();
        var tenantId = await _tenantSecretKeyValidator.GetTenantIdAsync(secretKey, Context.RequestAborted);

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return AuthenticateResult.Fail("Send a valid X-Secret-Key header.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, tenantId),
            new Claim("tenant_id", tenantId)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
