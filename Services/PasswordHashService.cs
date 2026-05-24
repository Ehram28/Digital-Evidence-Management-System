using System.Buffers.Binary;
using System.Security.Cryptography;

namespace DigitalEvidenceManagementSystem.Services;

public sealed class PasswordHashService
{
    private const byte CurrentVersion = 1;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int IterationCount = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public byte[] HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, IterationCount, Algorithm, HashSize);
        var payload = new byte[1 + sizeof(int) + SaltSize + HashSize];

        payload[0] = CurrentVersion;
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, sizeof(int)), IterationCount);
        salt.CopyTo(payload.AsSpan(1 + sizeof(int), SaltSize));
        hash.CopyTo(payload.AsSpan(1 + sizeof(int) + SaltSize, HashSize));

        return payload;
    }

    public bool VerifyPassword(byte[]? storedHash, string password)
    {
        if (storedHash is null || storedHash.Length != 1 + sizeof(int) + SaltSize + HashSize)
        {
            return false;
        }

        if (storedHash[0] != CurrentVersion)
        {
            return false;
        }

        var iterations = BinaryPrimitives.ReadInt32BigEndian(storedHash.AsSpan(1, sizeof(int)));
        if (iterations < 10_000)
        {
            return false;
        }

        var salt = storedHash.AsSpan(1 + sizeof(int), SaltSize);
        var expectedHash = storedHash.AsSpan(1 + sizeof(int) + SaltSize, HashSize);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, HashSize);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
