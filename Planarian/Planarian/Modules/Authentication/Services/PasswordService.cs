using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Planarian.Model.Shared.Helpers;

namespace Planarian.Modules.Authentication.Services;

public static class PasswordService
{
    private const int LegacyKeySize = 32;
    private const int CurrentIterations = 220_000;
    private static readonly object PasswordHasherUser = new();
    private static readonly PasswordHasher<object> PasswordHasher = new(Options.Create(new PasswordHasherOptions
    {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
        IterationCount = CurrentIterations
    }));

    public static string Hash(string password) => PasswordHasher.HashPassword(PasswordHasherUser, password);

    public static (bool Verified, bool NeedsUpgrade) Check(string hash, string password)
    {
        if (string.IsNullOrWhiteSpace(hash)) return (false, false);

        if (!hash.Contains('.', StringComparison.Ordinal))
        {
            try
            {
                var result = PasswordHasher.VerifyHashedPassword(PasswordHasherUser, hash, password);
                return result switch
                {
                    PasswordVerificationResult.Success => (true, false),
                    PasswordVerificationResult.SuccessRehashNeeded => (true, true),
                    _ => (false, false)
                };
            }
            catch (FormatException)
            {
                return (false, false);
            }
        }

        return CheckLegacyHash(hash, password);
    }

    private static (bool Verified, bool NeedsUpgrade) CheckLegacyHash(string hash, string password)
    {
        try
        {
            var parts = hash.Split('.', 3);
            if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations) || iterations <= 0)
                return (false, false);

            var salt = Convert.FromBase64String(parts[1]);
            var key = Convert.FromBase64String(parts[2]);
            if (key.Length != LegacyKeySize) return (false, false);

            var keyToCheck = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, key.Length);
            var verified = CryptographicOperations.FixedTimeEquals(keyToCheck, key);
            return (verified, verified);
        }
        catch (FormatException)
        {
            return (false, false);
        }
        catch (OverflowException)
        {
            return (false, false);
        }
    }

    public static string GenerateResetCode() => IdGenerator.GenerateSecureToken();
}
