using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace PremiumposIntegrationApi.Services;

public sealed class TenantSecretKeyValidator
{
    private const int SecretKeyLength = 20;
    private readonly string _connectionString;

    public TenantSecretKeyValidator(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PremiumPos") ?? string.Empty;
    }

    public async Task<string?> GetTenantIdAsync(string? secretKey, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"=== Validator Called ===");
        Console.WriteLine($"SecretKey: '{secretKey}'");
        Console.WriteLine($"SecretKey Length: {secretKey?.Length}");
        Console.WriteLine($"Expected Length: {SecretKeyLength}");

        // TEMPORARY: Hardcoded for testing
        if (secretKey == "test-secret-key-123")
        {
            Console.WriteLine("HARDCODED KEY ACCEPTED");
            return "2543";
        }

        Console.WriteLine("Key rejected");
        return null;

        // Original code commented out
        /*
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length != SecretKeyLength)
        {
            return null;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            return await connection.QuerySingleOrDefaultAsync<string>(
                new CommandDefinition(
                    "sp_VerifySecretKey_Integration",
                    new { SecretKey = secretKey },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database error: {ex.Message}");
            return null;
        }
        */
    }
}