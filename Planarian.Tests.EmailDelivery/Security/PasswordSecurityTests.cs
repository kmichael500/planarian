using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Authentication.Services;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Security;

public sealed class PasswordSecurityTests
{
    private const string LegacyHash = "10000.AAECAwQFBgcICQoLDA0ODw==.Xv2Q+hmLWXu5uoo++dUls9/yCXUSo7+04VqibFg1bwc=";
    private const string LegacyPassword = "LegacyPassword!123";

    [Fact]
    public void ExistingLegacyHashStillAuthenticatesAndRequiresMigration()
    {
        var result = PasswordService.Check(LegacyHash, LegacyPassword);
        Assert.True(result.Verified);
        Assert.True(result.NeedsUpgrade);
    }

    [Fact]
    public void ExistingLegacyHashRejectsWrongPasswordWithoutThrowing()
    {
        var result = PasswordService.Check(LegacyHash, "WrongPassword!123");
        Assert.False(result.Verified);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-password-hash")]
    [InlineData("10000.not-base64.not-base64")]
    public void MalformedStoredHashesFailClosed(string storedHash)
    {
        var result = PasswordService.Check(storedHash, LegacyPassword);

        Assert.False(result.Verified);
        Assert.False(result.NeedsUpgrade);
    }

    [Fact]
    public void NewlyCreatedHashUsesCurrentVersionedFormatAndFitsDatabaseColumn()
    {
        var hash = PasswordService.Hash(LegacyPassword);
        Assert.DoesNotContain(".", hash);
        Assert.True(hash.Length <= PropertyLength.PasswordHash);

        var bytes = Convert.FromBase64String(hash);
        Assert.Equal(0x01, bytes[0]);
        Assert.Equal(220_000, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(5, 4)));

        var result = PasswordService.Check(hash, LegacyPassword);
        Assert.True(result.Verified);
        Assert.False(result.NeedsUpgrade);
    }

    [Fact]
    public void OlderIdentityHashRequestsRehashWithoutBreakingAuthentication()
    {
        var oldHasher = new PasswordHasher<object>(Options.Create(new PasswordHasherOptions
        {
            IterationCount = 100_000
        }));
        var oldHash = oldHasher.HashPassword(new object(), LegacyPassword);
        var result = PasswordService.Check(oldHash, LegacyPassword);
        Assert.True(result.Verified);
        Assert.True(result.NeedsUpgrade);
    }

    [Fact]
    public void PasswordResetCodeUsesDedicated64CharacterHexFormatWithoutChangingInvitationLength()
    {
        var resetCode = PasswordService.GenerateResetCode();
        Assert.Equal(64, resetCode.Length);
        Assert.All(resetCode, c => Assert.True(Uri.IsHexDigit(c)));
        Assert.Equal(10, IdGenerator.Generate(PropertyLength.InvitationCode).Length);
    }
}
