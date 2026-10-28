using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Xunit;

namespace Planarian.Tests.Integration.Infrastructure;

public sealed class SaveChangesInterceptorUserLifecycleIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task AnonymousRegistrationSucceeds()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(AnonymousRegistrationSucceeds));

        await using (var registration = database.CreateDbContext("", null))
        {
            registration.Users.Add(new User("New", "User", "new-user@example.test")
            {
                Id = "newuser001",
                HashedPassword = "hash",
                EmailConfirmationCode = "confirm001"
            });
            await registration.SaveChangesAsync();
        }

        await using var verify = database.CreateDbContext("verify", null);
        Assert.True(await verify.Users.AnyAsync(user => user.Id == "newuser001"));
    }

    [Fact]
    public async Task AnonymousEmailConfirmationSucceeds()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(AnonymousEmailConfirmationSucceeds));
        await SeedUserAsync(database, "confirmusr");

        await using (var confirmation = database.CreateDbContext("", null))
        {
            var user = await confirmation.Users.SingleAsync(row => row.Id == "confirmusr");
            user.EmailConfirmedOn = DateTime.UtcNow;
            user.EmailConfirmationCode = null;
            await confirmation.SaveChangesAsync();
        }

        await using var verify = database.CreateDbContext("verify", null);
        var persisted = await verify.Users.SingleAsync(user => user.Id == "confirmusr");
        Assert.NotNull(persisted.EmailConfirmedOn);
        Assert.Null(persisted.EmailConfirmationCode);
    }

    [Fact]
    public async Task AnonymousPasswordResetRequestSucceeds()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(AnonymousPasswordResetRequestSucceeds));
        await SeedUserAsync(database, "resetreq01");

        await using (var resetRequest = database.CreateDbContext("", null))
        {
            var user = await resetRequest.Users.SingleAsync(row => row.Id == "resetreq01");
            user.PasswordResetCode = "reset001";
            user.PasswordResetCodeExpiration = DateTime.UtcNow.AddHours(1);
            await resetRequest.SaveChangesAsync();
        }

        await using var verify = database.CreateDbContext("verify", null);
        var persisted = await verify.Users.SingleAsync(user => user.Id == "resetreq01");
        Assert.Equal("reset001", persisted.PasswordResetCode);
        Assert.NotNull(persisted.PasswordResetCodeExpiration);
    }

    [Fact]
    public async Task AnonymousPasswordResetCompletionSucceeds()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(AnonymousPasswordResetCompletionSucceeds));
        await SeedUserAsync(database, "resetdone1", passwordResetCode: "reset002");

        await using (var completion = database.CreateDbContext("", null))
        {
            var user = await completion.Users.SingleAsync(row => row.Id == "resetdone1");
            user.HashedPassword = "new-hash";
            user.SessionVersion++;
            user.PasswordResetCode = null;
            user.PasswordResetCodeExpiration = null;
            await completion.SaveChangesAsync();
        }

        await using var verify = database.CreateDbContext("verify", null);
        var persisted = await verify.Users.SingleAsync(user => user.Id == "resetdone1");
        Assert.Equal("new-hash", persisted.HashedPassword);
        Assert.Null(persisted.PasswordResetCode);
        Assert.Null(persisted.PasswordResetCodeExpiration);
    }

    [Fact]
    public async Task AnonymousArbitraryUserEditIsRejected()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(AnonymousArbitraryUserEditIsRejected));
        await SeedUserAsync(database, "editdeny01");

        await using (var attack = database.CreateDbContext("", null))
        {
            var user = await attack.Users.SingleAsync(row => row.Id == "editdeny01");
            user.FirstName = "Attack";
            var exception = await Assert.ThrowsAsync<NullReferenceException>(() => attack.SaveChangesAsync());
            Assert.Equal("RequestUser or its Id is null.", exception.Message);
        }

        await using var verify = database.CreateDbContext("verify", null);
        Assert.Equal("Seed",
            await verify.Users.Where(user => user.Id == "editdeny01").Select(user => user.FirstName).SingleAsync());
    }

    [Fact]
    public async Task LoggedInUserWithoutSelectedAccountCannotModifyAnotherUser()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(LoggedInUserWithoutSelectedAccountCannotModifyAnotherUser));
        await SeedUserAsync(database, "victim0001");

        await using (var attack = database.CreateDbContext("membera001", null))
        {
            var user = await attack.Users.SingleAsync(row => row.Id == "victim0001");
            user.FirstName = "Hijacked";
            var exception = await Assert.ThrowsAsync<ApiException>(() => attack.SaveChangesAsync());
            Assert.Equal(403, exception.StatusCode);
        }

        await using var verify = database.CreateDbContext("verify", null);
        Assert.Equal("Seed",
            await verify.Users.Where(user => user.Id == "victim0001").Select(user => user.FirstName).SingleAsync());
    }

    [Fact]
    public async Task LoggedInUserWithoutSelectedAccountCanModifySelf()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(LoggedInUserWithoutSelectedAccountCanModifySelf));
        await SeedUserAsync(database, "membera001");

        await using (var self = database.CreateDbContext("membera001", null))
        {
            var user = await self.Users.SingleAsync(row => row.Id == "membera001");
            user.FirstName = "Updated";
            await self.SaveChangesAsync();
        }

        await using var verify = database.CreateDbContext("verify", null);
        Assert.Equal("Updated",
            await verify.Users.Where(user => user.Id == "membera001").Select(user => user.FirstName).SingleAsync());
    }

    [Fact]
    public async Task AnonymousTemporaryUserDeleteWithoutInvitationMembershipDeleteIsRejected()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AnonymousTemporaryUserDeleteWithoutInvitationMembershipDeleteIsRejected));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        const string temporaryUserId = "tempusr001";

        await using (var seed = database.CreateDbContext("manager", account.AccountId))
        {
            seed.Users.Add(new User("Temp", "Invite", "temp-invite@example.test")
            {
                Id = temporaryUserId,
                IsTemporary = true
            });
            seed.AccountUsers.Add(new AccountUser
            {
                AccountId = account.AccountId,
                UserId = temporaryUserId,
                InvitationCode = "invite001"
            });
            await seed.SaveChangesAsync();
        }

        await using (var attack = database.CreateDbContext("", null))
        {
            var temporary = await attack.Users.SingleAsync(row => row.Id == temporaryUserId);
            attack.Users.Remove(temporary);
            var exception = await Assert.ThrowsAsync<NullReferenceException>(() => attack.SaveChangesAsync());
            Assert.Equal("RequestUser or its Id is null.", exception.Message);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.True(await verify.Users.AnyAsync(row => row.Id == temporaryUserId));
        Assert.True(await verify.AccountUsers.AnyAsync(row =>
            row.AccountId == account.AccountId && row.UserId == temporaryUserId));
    }

    private static async Task SeedUserAsync(
        PostgresTestDatabase database,
        string userId,
        string? passwordResetCode = null)
    {
        await using var seed = database.CreateDbContext(userId, null);
        seed.Users.Add(new User("Seed", "User", $"{userId}@integration.test")
        {
            Id = userId,
            HashedPassword = "hash",
            EmailConfirmationCode = "confirm001",
            PasswordResetCode = passwordResetCode,
            PasswordResetCodeExpiration = passwordResetCode is null ? null : DateTime.UtcNow.AddHours(1)
        });
        await seed.SaveChangesAsync();
    }
}
