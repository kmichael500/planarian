using Planarian.Model.Shared;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Migrations;

public sealed class AuthenticationMigrationContractTests
{
    [Fact]
    public void V31PreservesExistingUsersWhileAddingAuthenticationHardeningState()
    {
        var source = ReadV31();

        Assert.Contains("name: \"SessionVersion\"", source);
        Assert.Contains("nullable: false,\n                defaultValue: 0", source);
        Assert.Contains("name: \"PendingEmailAddress\"", source);
        Assert.Contains($"maxLength: {PropertyLength.EmailAddress},\n                nullable: true", source);
        Assert.DoesNotContain("CreateIndex", source);
    }

    [Fact]
    public void V31ExpandsOnlyPasswordResetCodeStorage()
    {
        var source = ReadV31();

        Assert.Contains("name: \"PasswordResetCode\"", source);
        Assert.Contains($"maxLength: {PropertyLength.PasswordResetCode}", source);
        Assert.Contains($"oldMaxLength: {PropertyLength.InvitationCode}", source);
        Assert.Equal(10, PropertyLength.InvitationCode);
    }

    private static string ReadV31()
    {
        var migrationsDirectory = Path.Combine(FindRepositoryRoot(), "Planarian", "Planarian.Migrations", "Migrations");
        var migrationPath = Assert.Single(Directory.GetFiles(migrationsDirectory, "*_v31.cs"));
        return File.ReadAllText(migrationPath);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))) return directory.FullName;
        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
