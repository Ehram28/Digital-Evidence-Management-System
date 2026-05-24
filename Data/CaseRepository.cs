using DigitalEvidenceManagementSystem.Models;
using DigitalEvidenceManagementSystem.Models.ViewModels;
using Microsoft.Data.SqlClient;

namespace DigitalEvidenceManagementSystem.Data;

public sealed class CaseRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public CaseRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CaseRecord>> SearchAsync(string? search, string? status, string? priority, int take = 100, CancellationToken cancellationToken = default)
    {
        var cases = new List<CaseRecord>();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (@Take)
                   c.CaseId,
                   c.CaseNumber,
                   c.Title,
                   c.Description,
                   c.Status,
                   c.Priority,
                   c.AssignedExaminerId,
                   u.DisplayName AS AssignedExaminerName,
                   c.ClosedByUserId,
                   closedBy.DisplayName AS ClosedByName,
                   c.ResolutionReason,
                   c.CreatedAtUtc,
                   c.UpdatedAtUtc,
                   c.ClosedAtUtc,
                   COUNT(e.EvidenceId) AS EvidenceCount
            FROM dbo.Cases c
            LEFT JOIN dbo.Users u ON u.UserId = c.AssignedExaminerId
            LEFT JOIN dbo.Users closedBy ON closedBy.UserId = c.ClosedByUserId
            LEFT JOIN dbo.Evidence e ON e.CaseId = c.CaseId
            WHERE (@Search IS NULL OR c.CaseNumber LIKE '%' + @Search + '%' OR c.Title LIKE '%' + @Search + '%')
              AND (@Status IS NULL OR c.Status = @Status)
              AND (@Priority IS NULL OR c.Priority = @Priority)
            GROUP BY c.CaseId, c.CaseNumber, c.Title, c.Description, c.Status, c.Priority,
                     c.AssignedExaminerId, u.DisplayName, c.ClosedByUserId, closedBy.DisplayName,
                     c.ResolutionReason, c.CreatedAtUtc, c.UpdatedAtUtc, c.ClosedAtUtc
            ORDER BY c.UpdatedAtUtc DESC;
            """;
        command.Parameters.AddWithValue("@Take", take);
        command.Parameters.AddWithValue("@Search", string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim());
        command.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(status) ? DBNull.Value : status);
        command.Parameters.AddWithValue("@Priority", string.IsNullOrWhiteSpace(priority) ? DBNull.Value : priority);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            cases.Add(MapCase(reader));
        }

        return cases;
    }

    public async Task<CaseRecord?> GetByIdAsync(int caseId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.CaseId,
                   c.CaseNumber,
                   c.Title,
                   c.Description,
                   c.Status,
                   c.Priority,
                   c.AssignedExaminerId,
                   u.DisplayName AS AssignedExaminerName,
                   c.ClosedByUserId,
                   closedBy.DisplayName AS ClosedByName,
                   c.ResolutionReason,
                   c.CreatedAtUtc,
                   c.UpdatedAtUtc,
                   c.ClosedAtUtc,
                   COUNT(e.EvidenceId) AS EvidenceCount
            FROM dbo.Cases c
            LEFT JOIN dbo.Users u ON u.UserId = c.AssignedExaminerId
            LEFT JOIN dbo.Users closedBy ON closedBy.UserId = c.ClosedByUserId
            LEFT JOIN dbo.Evidence e ON e.CaseId = c.CaseId
            WHERE c.CaseId = @CaseId
            GROUP BY c.CaseId, c.CaseNumber, c.Title, c.Description, c.Status, c.Priority,
                     c.AssignedExaminerId, u.DisplayName, c.ClosedByUserId, closedBy.DisplayName,
                     c.ResolutionReason, c.CreatedAtUtc, c.UpdatedAtUtc, c.ClosedAtUtc;
            """;
        command.Parameters.AddWithValue("@CaseId", caseId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapCase(reader) : null;
    }

    public async Task<int> CreateAsync(CaseCreateViewModel model, int assignedExaminerId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Cases (CaseNumber, Title, Description, Status, Priority, AssignedExaminerId)
            OUTPUT INSERTED.CaseId
            VALUES (@CaseNumber, @Title, @Description, @Status, @Priority, @AssignedExaminerId);
            """;
        command.Parameters.AddWithValue("@CaseNumber", model.CaseNumber.Trim());
        command.Parameters.AddWithValue("@Title", model.Title.Trim());
        command.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(model.Description) ? DBNull.Value : model.Description.Trim());
        command.Parameters.AddWithValue("@Status", model.Status);
        command.Parameters.AddWithValue("@Priority", model.Priority);
        command.Parameters.AddWithValue("@AssignedExaminerId", assignedExaminerId);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task UpdateAsync(CaseEditViewModel model, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Cases
            SET CaseNumber = @CaseNumber,
                Title = @Title,
                Description = @Description,
                Priority = @Priority,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE CaseId = @CaseId;
            """;
        command.Parameters.AddWithValue("@CaseId", model.CaseId);
        command.Parameters.AddWithValue("@CaseNumber", model.CaseNumber.Trim());
        command.Parameters.AddWithValue("@Title", model.Title.Trim());
        command.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(model.Description) ? DBNull.Value : model.Description.Trim());
        command.Parameters.AddWithValue("@Priority", model.Priority);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ResolveAsync(int caseId, string status, string reason, int closedByUserId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Cases
            SET Status = @Status,
                ResolutionReason = @ResolutionReason,
                ClosedByUserId = @ClosedByUserId,
                ClosedAtUtc = SYSUTCDATETIME(),
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE CaseId = @CaseId;
            """;
        command.Parameters.AddWithValue("@CaseId", caseId);
        command.Parameters.AddWithValue("@Status", status);
        command.Parameters.AddWithValue("@ResolutionReason", reason.Trim());
        command.Parameters.AddWithValue("@ClosedByUserId", closedByUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> DeleteIfEmptyAsync(int caseId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM dbo.Cases
            WHERE CaseId = @CaseId
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.Evidence
                  WHERE CaseId = @CaseId
              );
            """;
        command.Parameters.AddWithValue("@CaseId", caseId);
        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                SUM(CASE WHEN c.Status NOT IN ('Completed', 'Cancelled', 'Closed') THEN 1 ELSE 0 END) AS OpenCases,
                SUM(CASE WHEN c.Priority IN ('High', 'Critical') AND c.Status NOT IN ('Completed', 'Cancelled', 'Closed') THEN 1 ELSE 0 END) AS HighPriorityCases,
                (SELECT COUNT(*) FROM dbo.Evidence) AS EvidenceItems,
                (SELECT COUNT(*) FROM dbo.Evidence WHERE VerificationStatus = 'Mismatch') AS HashMismatches
            FROM dbo.Cases c;
            """;

        var dashboard = new DashboardViewModel();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                dashboard.OpenCases = reader.IsDBNull(reader.GetOrdinal("OpenCases")) ? 0 : Convert.ToInt32(reader["OpenCases"]);
                dashboard.HighPriorityCases = reader.IsDBNull(reader.GetOrdinal("HighPriorityCases")) ? 0 : Convert.ToInt32(reader["HighPriorityCases"]);
                dashboard.EvidenceItems = Convert.ToInt32(reader["EvidenceItems"]);
                dashboard.HashMismatches = Convert.ToInt32(reader["HashMismatches"]);
            }
        }

        dashboard.RecentCases = await SearchAsync(null, null, null, 5, cancellationToken);
        return dashboard;
    }

    private static CaseRecord MapCase(SqlDataReader reader)
    {
        return new CaseRecord
        {
            CaseId = reader.GetInt32(reader.GetOrdinal("CaseId")),
            CaseNumber = reader.GetString(reader.GetOrdinal("CaseNumber")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Description = reader.GetNullableString("Description"),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            Priority = reader.GetString(reader.GetOrdinal("Priority")),
            AssignedExaminerId = reader.GetNullableInt32("AssignedExaminerId"),
            AssignedExaminerName = reader.GetNullableString("AssignedExaminerName"),
            ClosedByUserId = reader.GetNullableInt32("ClosedByUserId"),
            ClosedByName = reader.GetNullableString("ClosedByName"),
            ResolutionReason = reader.GetNullableString("ResolutionReason"),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")),
            UpdatedAtUtc = reader.GetDateTime(reader.GetOrdinal("UpdatedAtUtc")),
            ClosedAtUtc = reader.GetNullableDateTime("ClosedAtUtc"),
            EvidenceCount = Convert.ToInt32(reader["EvidenceCount"])
        };
    }
}
