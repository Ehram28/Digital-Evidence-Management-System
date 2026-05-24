using DigitalEvidenceManagementSystem.Models;
using Microsoft.Data.SqlClient;

namespace DigitalEvidenceManagementSystem.Data;

public sealed class UserRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public UserRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<UserLoginRecord?> GetLoginByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var user = await FindByUsernameAsync(connection, username, cancellationToken);
        if (user is null)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT PasswordHash
            FROM dbo.Users
            WHERE UserId = @UserId;
            """;
        command.Parameters.AddWithValue("@UserId", user.UserId);

        var passwordHash = await command.ExecuteScalarAsync(cancellationToken) as byte[];
        return new UserLoginRecord(user, passwordHash);
    }

    public async Task RecordLoginAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Users
            SET LastLoginAtUtc = SYSUTCDATETIME()
            WHERE UserId = @UserId;
            """;
        command.Parameters.AddWithValue("@UserId", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AppUser> CreateAsync(
        string username,
        string displayName,
        string? email,
        byte[] passwordHash,
        string roleName,
        string roleDescription,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DECLARE @RoleId INT;
            SELECT @RoleId = RoleId
            FROM dbo.Roles
            WHERE RoleName = @RoleName;

            IF @RoleId IS NULL
            BEGIN
                INSERT INTO dbo.Roles (RoleName, Description)
                VALUES (@RoleName, @RoleDescription);

                SET @RoleId = CONVERT(INT, SCOPE_IDENTITY());
            END;

            DECLARE @InsertedUsers TABLE (UserId INT NOT NULL);

            INSERT INTO dbo.Users (Username, DisplayName, Email, PasswordHash, IsActive)
            OUTPUT INSERTED.UserId INTO @InsertedUsers
            VALUES (@Username, @DisplayName, @Email, @PasswordHash, 1);

            INSERT INTO dbo.UserRoles (UserId, RoleId)
            SELECT UserId, @RoleId
            FROM @InsertedUsers;

            SELECT UserId
            FROM @InsertedUsers;
            """;
        command.Parameters.AddWithValue("@Username", username);
        command.Parameters.AddWithValue("@DisplayName", displayName);
        command.Parameters.AddWithValue("@Email", (object?)email ?? DBNull.Value);
        command.Parameters.AddWithValue("@PasswordHash", passwordHash);
        command.Parameters.AddWithValue("@RoleName", roleName);
        command.Parameters.AddWithValue("@RoleDescription", roleDescription);

        var userId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        await transaction.CommitAsync(cancellationToken);

        return new AppUser
        {
            UserId = userId,
            Username = username,
            DisplayName = displayName,
            Email = email,
            IsActive = true,
            Roles = new[] { roleName }
        };
    }

    public async Task<AppUser> GetOrCreateByUsernameAsync(string username, string displayName, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var existing = await FindByUsernameAsync(connection, username, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
            INSERT INTO dbo.Users (Username, DisplayName, IsActive)
            OUTPUT INSERTED.UserId
            VALUES (@Username, @DisplayName, 1);
            """;
        insertCommand.Parameters.AddWithValue("@Username", username);
        insertCommand.Parameters.AddWithValue("@DisplayName", displayName);

        var userId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync(cancellationToken));
        return new AppUser
        {
            UserId = userId,
            Username = username,
            DisplayName = displayName,
            IsActive = true,
            Roles = Array.Empty<string>()
        };
    }

    private static async Task<AppUser?> FindByUsernameAsync(SqlConnection connection, string username, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT UserId, Username, DisplayName, Email, IsActive
            FROM dbo.Users
            WHERE Username = @Username;
            """;
        command.Parameters.AddWithValue("@Username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var user = new AppUser
        {
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            Username = reader.GetString(reader.GetOrdinal("Username")),
            DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
            Email = reader.GetNullableString("Email"),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
        };

        await reader.CloseAsync();
        user.Roles = await GetRolesAsync(connection, user.UserId, cancellationToken);
        return user;
    }

    private static async Task<IReadOnlyList<string>> GetRolesAsync(SqlConnection connection, int userId, CancellationToken cancellationToken)
    {
        var roles = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.RoleName
            FROM dbo.UserRoles ur
            INNER JOIN dbo.Roles r ON r.RoleId = ur.RoleId
            WHERE ur.UserId = @UserId
            ORDER BY r.RoleName;
            """;
        command.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(reader.GetString(reader.GetOrdinal("RoleName")));
        }

        return roles;
    }
}

public sealed record UserLoginRecord(AppUser User, byte[]? PasswordHash);
