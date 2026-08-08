using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace PremiumposIntegrationApi.Services;

/// <summary>
/// Validates an integration secret key against the Premium POS database.
/// The stored procedure must accept <c>@SecretKey</c> and return the tenant ID
/// as the first column of a single row. No row means that the key is invalid.
/// </summary>
public sealed class TenantSecretKeyValidator
{
    private const int SecretKeyLength = 20;
    private readonly string _connectionString;

    public TenantSecretKeyValidator(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PremiumPos") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException(
                "The Premium POS database connection string is missing. Set ConnectionStrings__PremiumPos or configure the ConnectionStrings:PremiumPos user secret.");
        }
    }

    public async Task<string?> GetTenantIdAsync(string? secretKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length != SecretKeyLength)
        {
            return null;
        }

        await using var connection = new SqlConnection(_connectionString);

        return await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "sp_VerifySecretKey_Integration",
                new { SecretKey = secretKey },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
    }
}
