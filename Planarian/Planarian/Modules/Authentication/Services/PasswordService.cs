using System.Security.Cryptography;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;

namespace Planarian.Modules.Authentication.Services;

public static class PasswordService
{
    private const int SaltSize = 16; // 128 bit 
    private const int KeySize = 32; // 256 bit
    private const int Iterations = 10000;

    public static string Hash(string password)
    {
        using var algorithm = new Rfc2898DeriveBytes(
            password,
            SaltSize,
            Iterations,
            HashAlgorithmName.SHA512);
        var key = Convert.ToBase64String(algorithm.GetBytes(KeySize));
        var salt = Convert.ToBase64String(algorithm.Salt);

        return $"{Iterations}.{salt}.{key}";
    }

    public static (bool Verified, bool NeedsUpgrade) Check(string hash, string password)
    {
        var parts = hash.Split('.', 3);

        if (parts.Length != 3)
            throw new FormatException("Unexpected hash format. " +
                                      "Should be formatted as `{iterations}.{salt}.{hash}`");

        var iterations = Convert.ToInt32(parts[0]);
        var salt = Convert.FromBase64String(parts[1]);
        var key = Convert.FromBase64String(parts[2]);

        var needsUpgrade = iterations != iterations;

        using var algorithm = new Rfc2898DeriveBytes(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA512);
        var keyToCheck = algorithm.GetBytes(KeySize);

        var verified = keyToCheck.SequenceEqual(key);

        return (verified, needsUpgrade);
    }

    public static string GenerateResetCode()
    {
        return IdGenerator.Generate(PropertyLength.PasswordResetCode);
    }
}