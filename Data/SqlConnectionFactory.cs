using Microsoft.Data.SqlClient;

namespace DigitalEvidenceManagementSystem.Data;

public sealed class SqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection.");
    }

    public SqlConnection CreateConnection() => new(_connectionString);
}
