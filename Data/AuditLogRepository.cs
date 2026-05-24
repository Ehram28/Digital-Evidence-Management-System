namespace DigitalEvidenceManagementSystem.Data;

public sealed class AuditLogRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public AuditLogRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AddAsync(
        int? userId,
        string username,
        string eventType,
        string? entityType,
        string? entityId,
        string? ipAddress,
        string? userAgent,
        string? details,
        bool succeeded,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.AccessAuditLogs
                (UserId, Username, EventType, EntityType, EntityId, IpAddress, UserAgent, Details, Succeeded)
            VALUES
                (@UserId, @Username, @EventType, @EntityType, @EntityId, @IpAddress, @UserAgent, @Details, @Succeeded);
            """;
        command.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
        command.Parameters.AddWithValue("@Username", username);
        command.Parameters.AddWithValue("@EventType", eventType);
        command.Parameters.AddWithValue("@EntityType", (object?)entityType ?? DBNull.Value);
        command.Parameters.AddWithValue("@EntityId", (object?)entityId ?? DBNull.Value);
        command.Parameters.AddWithValue("@IpAddress", (object?)ipAddress ?? DBNull.Value);
        command.Parameters.AddWithValue("@UserAgent", (object?)userAgent ?? DBNull.Value);
        command.Parameters.AddWithValue("@Details", (object?)details ?? DBNull.Value);
        command.Parameters.AddWithValue("@Succeeded", succeeded);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
