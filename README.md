# Premium POS Integration API

An ASP.NET Core API that connects Premium POS orders to:

- **Moyassar** for SAR payment creation and payment-status webhooks.
- **Redbox** for shipment creation and delivery-status webhooks.

The API keeps payment, shipment, and payment-log records in an EF Core in-memory database. It is intended as an integration service or development starter; data is lost whenever the application restarts.

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Valid Moyassar and Redbox API credentials for any real provider calls

## Run locally

```powershell
dotnet restore
dotnet run --project .\PremiumposIntegrationApi
```

The development launch profile exposes the service at:

- HTTP: `http://localhost:5137`
- HTTPS: `https://localhost:7196`

Check that it is running:

```powershell
Invoke-RestMethod http://localhost:5137/health
```

In the Development environment, the OpenAPI document is available at `/openapi/v1.json`.

## Postman

Import [the Postman collection](postman/Premium-POS-Integration.postman_collection.json) and, for local use, [the local environment template](postman/Premium-POS-Integration.local.postman_environment.json). Select the imported environment before sending requests. The webhook requests automatically calculate the required HMAC signature; replace their sample provider IDs with IDs returned by the corresponding create request.

## Configuration

Configuration is loaded from `appsettings.json` and the environment-specific `appsettings.{Environment}.json` file. Configure these values before connecting to real services:

| Setting | Purpose |
| --- | --- |
| `Moyassar:BaseUrl` | Moyassar API base URL |
| `Moyassar:ApiKey` | Moyassar secret API key |
| `Moyassar:WebhookSecret` | Shared secret for validating `X-Moyassar-Signature` |
| `Moyassar:TestCard:*` | Card data sent by the current payment-creation flow |
| `Redbox:BaseUrl` | Redbox API base URL |
| `Redbox:ApiKey` | Redbox bearer token |
| `Redbox:WebhookSecret` | Shared secret for validating `X-Redbox-Signature` |
| `ConnectionStrings:PremiumPos` | SQL Server connection string used to validate integration secret keys |

For local overrides, use environment variables with double underscores:

```powershell
$env:MOYASSAR_API_KEY = "your-secret-key"
$env:Redbox__ApiKey = "your-redbox-key"
$env:Redbox__BaseUrl = "https://your-redbox-api/"
$env:ConnectionStrings__PremiumPos = "Data Source=your-server;Initial Catalog=your-database;User ID=your-user;Password=your-password;Encrypt=True;TrustServerCertificate=False"
```

For Visual Studio debugging, user secrets are often more convenient because they are loaded automatically in the Development environment and are not stored in the repository:

```powershell
dotnet user-secrets set "ConnectionStrings:PremiumPos" "Data Source=your-server;Initial Catalog=your-database;User ID=your-user;Password=your-password;Encrypt=True;TrustServerCertificate=False" --project .\PremiumposIntegrationApi
```

The validation stored procedure must accept `@SecretKey` and return the authenticated tenant ID as the first column of one row. Returning no row denies the request.

> **Security:** This repository currently contains credential-like test values in its settings files. Rotate any keys that have been used outside a local test environment, and keep production secrets in a secret store or deployment environment variables rather than source control.

## API

### Health check

`GET /health`

Returns `{ "status": "ok" }`.

### Create a payment

`POST /api/payments/create`

Supply `X-Secret-Key` with the 20-character integration secret. An ASP.NET Core authentication handler passes it to the SQL Server stored procedure `sp_VerifySecretKey_Integration`; the tenant ID returned by that procedure is available as an authenticated claim and is used for the payment record and Moyassar metadata. Invalid or missing keys return `401 Unauthorized`.

```powershell
$body = @{
  orderId = "ORDER-1001"
  amount = 125.50
  returnUrl = "https://example.com/payment/complete"
} | ConvertTo-Json

Invoke-RestMethod http://localhost:5137/api/payments/create `
  -Method Post `
  -Headers @{ "X-Secret-Key" = "your-20-character-key" } `
  -ContentType "application/json" `
  -Body $body
```

The service creates a Moyassar payment in SAR, records the result, and returns a pending/failed status, the local payment ID, provider payment ID, redirect URL, and a serialized integration log. Amounts are supplied in SAR; the provider request converts them to halalas.

### Moyassar webhook

`POST /api/payments/webhook`

Provide the raw provider payload and an `X-Moyassar-Signature` header containing the lowercase HMAC-SHA256 hex digest of that payload using `Moyassar:WebhookSecret`. The service marks matching payments as `Paid` when the provider status is `paid`; all other statuses mark them as `Failed`.

### Create a shipment

`POST /api/deliveries/create`

Supply the same `X-Secret-Key` header used for payment creation.

```json
{
  "orderId": "ORDER-1001",
  "recipient": "Customer name",
  "address": "Full delivery address",
  "weightKg": 1.5
}
```

The service forwards the request to Redbox and returns local/provider shipment IDs and the tracking URL.

### Redbox webhook

`POST /api/deliveries/webhook`

Provide `X-Redbox-Signature` using the same HMAC-SHA256 scheme, with `Redbox:WebhookSecret`. A payload with `status` set to `delivered` marks the matching shipment as delivered.

## Notes for production

- Replace EF Core's in-memory database with a persistent provider; records and idempotency state disappear on restart.
- Configure public HTTPS webhook URLs with the providers and verify their exact payload/signature contracts.
- Store and rotate all API and webhook secrets outside the repository.
- Add authentication/authorization and validation appropriate to your POS and tenant model. Payment and delivery creation are protected by database-backed secret-key authentication. Webhooks are exempt from that scheme because they authenticate through their provider HMAC signatures.

## Project layout

```text
PremiumposIntegrationApi/
  Controllers/       HTTP endpoints
  Services/          Moyassar, Redbox, payment, and delivery workflows
  Models.cs          Payment, shipment, and log entities
  AppDbContext.cs    EF Core data context
  Program.cs         Service registration, middleware, and routes
```
