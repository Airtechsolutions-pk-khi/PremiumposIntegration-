using System.Data;
using Microsoft.Data.SqlClient;

namespace PremiumposIntegrationApi.Services;

public sealed class DbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PremiumPos")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is missing. Set it in appsettings.json.");
    }

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}