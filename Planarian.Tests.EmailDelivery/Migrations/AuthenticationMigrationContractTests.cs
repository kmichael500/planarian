using Planarian.Model.Shared;
using Planarian.Tests.EmailDelivery.TestInfrastructure;
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

    [Fact]
    public void V33AddsNullableDisablingStateAndRefusesUnsafeDowngrade()
    {
        var migrationsDirectory = Path.Combine(RepositoryPaths.Root,
            "Planarian", "Planarian.Migrations", "Migrations");
        var source = File.ReadAllText(Assert.Single(Directory.GetFiles(migrationsDirectory, "*_v33.cs")));

        Assert.Contains("name: \"AccessDisabledOn\"", source);
        Assert.Contains("nullable: true", source);
        Assert.Contains("RAISE EXCEPTION", source);
        Assert.Contains("WHERE \"AccessDisabledOn\" IS NOT NULL", source);
        Assert.Contains("DropColumn", source);
        Assert.DoesNotContain("DeleteData", source);
    }

    private static string ReadV31()
    {
        var migrationsDirectory = Path.Combine(RepositoryPaths.Root,
            "Planarian", "Planarian.Migrations", "Migrations");
        var migrationPath = Assert.Single(Directory.GetFiles(migrationsDirectory, "*_v31.cs"));
        return File.ReadAllText(migrationPath);
    }

}
