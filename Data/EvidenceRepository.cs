using DigitalEvidenceManagementSystem.Models;
using Microsoft.Data.SqlClient;

namespace DigitalEvidenceManagementSystem.Data;

public sealed class EvidenceRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public EvidenceRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> CreateEvidenceWithCustodyAsync(EvidenceItem evidence, ChainOfCustodyEntry custodyEntry, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqlTransaction)transaction;
            command.CommandText = """
                INSERT INTO dbo.Evidence
                    (CaseId, UploadedByUserId, OriginalFileName, StoredFileName, StoragePath, ContentType,
                     EvidenceType, FileSizeBytes, Sha256Hash, IsOriginalMedia, VerificationStatus, Notes)
                OUTPUT INSERTED.EvidenceId
                VALUES
                    (@CaseId, @UploadedByUserId, @OriginalFileName, @StoredFileName, @StoragePath, @ContentType,
                     @EvidenceType, @FileSizeBytes, @Sha256Hash, @IsOriginalMedia, 'Verified', @Notes);
                """;
            command.Parameters.AddWithValue("@CaseId", evidence.CaseId);
            command.Parameters.AddWithValue("@UploadedByUserId", evidence.UploadedByUserId);
            command.Parameters.AddWithValue("@OriginalFileName", evidence.OriginalFileName);
            command.Parameters.AddWithValue("@StoredFileName", evidence.StoredFileName);
            command.Parameters.AddWithValue("@StoragePath", evidence.StoragePath);
            command.Parameters.AddWithValue("@ContentType", (object?)evidence.ContentType ?? DBNull.Value);
            command.Parameters.AddWithValue("@EvidenceType", evidence.EvidenceType);
            command.Parameters.AddWithValue("@FileSizeBytes", evidence.FileSizeBytes);
            command.Parameters.AddWithValue("@Sha256Hash", evidence.Sha256Hash);
            command.Parameters.AddWithValue("@IsOriginalMedia", evidence.IsOriginalMedia);
            command.Parameters.AddWithValue("@Notes", (object?)evidence.Notes ?? DBNull.Value);

            var evidenceId = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));

            custodyEntry.EvidenceId = evidenceId;
            await InsertCustodyEntryAsync(connection, (SqlTransaction)transaction, custodyEntry, cancellationToken);

            await using var caseCommand = connection.CreateCommand();
            caseCommand.Transaction = (SqlTransaction)transaction;
            caseCommand.CommandText = "UPDATE dbo.Cases SET UpdatedAtUtc = SYSUTCDATETIME() WHERE CaseId = @CaseId;";
            caseCommand.Parameters.AddWithValue("@CaseId", evidence.CaseId);
            await caseCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return evidenceId;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<EvidenceItem>> GetByCaseAsync(int caseId, CancellationToken cancellationToken = default)
    {
        var evidence = new List<EvidenceItem>();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = EvidenceSelectSql + " WHERE e.CaseId = @CaseId ORDER BY e.UploadedAtUtc DESC;";
        command.Parameters.AddWithValue("@CaseId", caseId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            evidence.Add(MapEvidence(reader));
        }

        return evidence;
    }

    public async Task<EvidenceItem?> GetByIdAsync(long evidenceId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = EvidenceSelectSql + " WHERE e.EvidenceId = @EvidenceId;";
        command.Parameters.AddWithValue("@EvidenceId", evidenceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapEvidence(reader) : null;
    }

    public async Task<IReadOnlyList<ChainOfCustodyEntry>> GetCustodyEntriesAsync(long evidenceId, CancellationToken cancellationToken = default)
    {
        var entries = new List<ChainOfCustodyEntry>();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT coc.CocId,
                   coc.EvidenceId,
                   coc.CaseId,
                   coc.ActionType,
                   coc.PerformedByUserId,
                   u.DisplayName AS PerformedByName,
                   coc.PerformedAtUtc,
                   coc.Location,
                   coc.NarrativeNote,
                   coc.FromCustodianId,
                   coc.ToCustodianId,
                   coc.FileHashAtAction,
                   coc.SourceIpAddress
            FROM dbo.ChainOfCustody coc
            INNER JOIN dbo.Users u ON u.UserId = coc.PerformedByUserId
            WHERE coc.EvidenceId = @EvidenceId
            ORDER BY coc.PerformedAtUtc DESC;
            """;
        command.Parameters.AddWithValue("@EvidenceId", evidenceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new ChainOfCustodyEntry
            {
                CocId = reader.GetInt64(reader.GetOrdinal("CocId")),
                EvidenceId = reader.GetInt64(reader.GetOrdinal("EvidenceId")),
                CaseId = reader.GetInt32(reader.GetOrdinal("CaseId")),
                ActionType = reader.GetString(reader.GetOrdinal("ActionType")),
                PerformedByUserId = reader.GetInt32(reader.GetOrdinal("PerformedByUserId")),
                PerformedByName = reader.GetString(reader.GetOrdinal("PerformedByName")),
                PerformedAtUtc = reader.GetDateTime(reader.GetOrdinal("PerformedAtUtc")),
                Location = reader.GetString(reader.GetOrdinal("Location")),
                NarrativeNote = reader.GetString(reader.GetOrdinal("NarrativeNote")),
                FromCustodianId = reader.GetNullableInt32("FromCustodianId"),
                ToCustodianId = reader.GetNullableInt32("ToCustodianId"),
                FileHashAtAction = reader.GetNullableString("FileHashAtAction"),
                SourceIpAddress = reader.GetNullableString("SourceIpAddress")
            });
        }

        return entries;
    }

    public async Task RecordCustodyEntryAsync(ChainOfCustodyEntry custodyEntry, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await InsertCustodyEntryAsync(connection, null, custodyEntry, cancellationToken);
    }

    public async Task UpdateVerificationStatusAsync(long evidenceId, string status, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Evidence
            SET VerificationStatus = @Status,
                LastVerifiedAtUtc = SYSUTCDATETIME()
            WHERE EvidenceId = @EvidenceId;
            """;
        command.Parameters.AddWithValue("@EvidenceId", evidenceId);
        command.Parameters.AddWithValue("@Status", status);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertCustodyEntryAsync(SqlConnection connection, SqlTransaction? transaction, ChainOfCustodyEntry custodyEntry, CancellationToken cancellationToken)
    {
        await using var custodyCommand = connection.CreateCommand();
        custodyCommand.Transaction = transaction;
        custodyCommand.CommandText = """
            INSERT INTO dbo.ChainOfCustody
                (EvidenceId, CaseId, ActionType, PerformedByUserId, Location, NarrativeNote,
                 FromCustodianId, ToCustodianId, FileHashAtAction, SourceIpAddress)
            VALUES
                (@EvidenceId, @CaseId, @ActionType, @PerformedByUserId, @Location, @NarrativeNote,
                 @FromCustodianId, @ToCustodianId, @FileHashAtAction, @SourceIpAddress);
            """;
        custodyCommand.Parameters.AddWithValue("@EvidenceId", custodyEntry.EvidenceId);
        custodyCommand.Parameters.AddWithValue("@CaseId", custodyEntry.CaseId);
        custodyCommand.Parameters.AddWithValue("@ActionType", custodyEntry.ActionType);
        custodyCommand.Parameters.AddWithValue("@PerformedByUserId", custodyEntry.PerformedByUserId);
        custodyCommand.Parameters.AddWithValue("@Location", custodyEntry.Location);
        custodyCommand.Parameters.AddWithValue("@NarrativeNote", custodyEntry.NarrativeNote);
        custodyCommand.Parameters.AddWithValue("@FromCustodianId", (object?)custodyEntry.FromCustodianId ?? DBNull.Value);
        custodyCommand.Parameters.AddWithValue("@ToCustodianId", (object?)custodyEntry.ToCustodianId ?? DBNull.Value);
        custodyCommand.Parameters.AddWithValue("@FileHashAtAction", (object?)custodyEntry.FileHashAtAction ?? DBNull.Value);
        custodyCommand.Parameters.AddWithValue("@SourceIpAddress", (object?)custodyEntry.SourceIpAddress ?? DBNull.Value);
        await custodyCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private const string EvidenceSelectSql = """
        SELECT e.EvidenceId,
               e.CaseId,
               c.CaseNumber,
               e.UploadedByUserId,
               u.DisplayName AS UploadedByName,
               e.OriginalFileName,
               e.StoredFileName,
               e.StoragePath,
               e.ContentType,
               e.EvidenceType,
               e.FileSizeBytes,
               e.Sha256Hash,
               e.IsOriginalMedia,
               e.UploadedAtUtc,
               e.LastVerifiedAtUtc,
               e.VerificationStatus,
               e.Notes
        FROM dbo.Evidence e
        INNER JOIN dbo.Cases c ON c.CaseId = e.CaseId
        INNER JOIN dbo.Users u ON u.UserId = e.UploadedByUserId
        """;

    private static EvidenceItem MapEvidence(SqlDataReader reader)
    {
        return new EvidenceItem
        {
            EvidenceId = reader.GetInt64(reader.GetOrdinal("EvidenceId")),
            CaseId = reader.GetInt32(reader.GetOrdinal("CaseId")),
            CaseNumber = reader.GetString(reader.GetOrdinal("CaseNumber")),
            UploadedByUserId = reader.GetInt32(reader.GetOrdinal("UploadedByUserId")),
            UploadedByName = reader.GetString(reader.GetOrdinal("UploadedByName")),
            OriginalFileName = reader.GetString(reader.GetOrdinal("OriginalFileName")),
            StoredFileName = reader.GetString(reader.GetOrdinal("StoredFileName")),
            StoragePath = reader.GetString(reader.GetOrdinal("StoragePath")),
            ContentType = reader.GetNullableString("ContentType"),
            EvidenceType = reader.GetString(reader.GetOrdinal("EvidenceType")),
            FileSizeBytes = reader.GetInt64(reader.GetOrdinal("FileSizeBytes")),
            Sha256Hash = reader.GetString(reader.GetOrdinal("Sha256Hash")),
            IsOriginalMedia = reader.GetBoolean(reader.GetOrdinal("IsOriginalMedia")),
            UploadedAtUtc = reader.GetDateTime(reader.GetOrdinal("UploadedAtUtc")),
            LastVerifiedAtUtc = reader.GetNullableDateTime("LastVerifiedAtUtc"),
            VerificationStatus = reader.GetString(reader.GetOrdinal("VerificationStatus")),
            Notes = reader.GetNullableString("Notes")
        };
    }
}
