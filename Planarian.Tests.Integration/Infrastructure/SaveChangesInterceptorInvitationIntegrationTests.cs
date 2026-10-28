using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Model.Shared.Helpers;
using Xunit;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Tests.Integration.Infrastructure;

public sealed class SaveChangesInterceptorInvitationIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task AccountUserCannotJoinAnotherAccountWithoutVerifiedInvitation()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AccountUserCannotJoinAnotherAccountWithoutVerifiedInvitation));
        var accountA = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var accountB = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'b');

        await using (var selectedAttack = database.CreateDbContext("membera", accountA.AccountId))
        {
            selectedAttack.AccountUsers.Add(new AccountUser
            { AccountId = accountB.AccountId, UserId = selectedAttack.RequestUser.Id });
            await AssertForbiddenAsync(selectedAttack);
        }

        await using (var noSelectionAttack = database.CreateDbContext("membera", null))
        {
            noSelectionAttack.AccountUsers.Add(new AccountUser
            {
                AccountId = accountB.AccountId,
                UserId = noSelectionAttack.RequestUser.Id,
                InvitationAcceptedOn = DateTime.UtcNow,
                InvitationSentOn = DateTime.UtcNow.AddMinutes(-5)
            });
            await AssertForbiddenAsync(noSelectionAttack);
        }

        await using var verify = database.CreateDbContext("verify-b", accountB.AccountId);
        Assert.False(await verify.AccountUsers.AnyAsync(row =>
            row.AccountId == accountB.AccountId && row.UserId == "membera"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VerifiedInvitationClaimWorksWithAnotherAccountSelectedAndTransfersOnlyInvitedPermissions(
        bool persistInvitationSentOn)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(VerifiedInvitationClaimWorksWithAnotherAccountSelectedAndTransfersOnlyInvitedPermissions)}-{persistInvitationSentOn}");
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        const string realUserId = "membera";
        const string temporaryUserId = "tempinv001";
        const string cavePermissionId = "caveperm01";
        const string userPermissionId = "userperm01";
        const string cavePermissionKeyId = "permview01";
        const string userPermissionKeyId = "permacct01";
        DateTime? invitationSentOn = persistInvitationSentOn ? DateTime.UtcNow.AddMinutes(-10) : null;

        await using (var seedRealUser = database.CreateDbContext(realUserId, accountA.AccountId))
        {
            seedRealUser.AccountUsers.Add(new AccountUser
            { AccountId = accountA.AccountId, UserId = realUserId });
            await seedRealUser.SaveChangesAsync();
        }

        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = cavePermissionKeyId,
            Name = "View",
            Description = "Invitation cave permission",
            Key = "invite-view",
            PermissionType = "Cave"
        });
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = userPermissionKeyId,
            Name = "Account permission",
            Description = "Invitation account permission",
            Key = "invite-account",
            PermissionType = "Account"
        });

        await using (var seedInvitation = database.CreateDbContext("managerb", accountB.AccountId))
        {
            seedInvitation.Users.Add(new User("Invited", "User", $"{realUserId}@integration.test")
            {
                Id = temporaryUserId,
                IsTemporary = true
            });
            seedInvitation.AccountUsers.Add(new AccountUser
            {
                AccountId = accountB.AccountId,
                UserId = temporaryUserId,
                InvitationCode = "invite003",
                InvitationSentOn = invitationSentOn
            });
            seedInvitation.CavePermissions.Add(new CavePermission
            {
                Id = cavePermissionId,
                AccountId = accountB.AccountId,
                UserId = temporaryUserId,
                PermissionId = cavePermissionKeyId,
                CaveId = accountB.CaveId
            });
            seedInvitation.UserPermissions.Add(new UserPermission
            {
                Id = userPermissionId,
                AccountId = accountB.AccountId,
                UserId = temporaryUserId,
                PermissionId = userPermissionKeyId
            });
            await seedInvitation.SaveChangesAsync();
        }

        await using (var accept = database.CreateDbContext(realUserId, accountA.AccountId))
        {
            var realUser = await accept.Users.SingleAsync(user => user.Id == realUserId);
            var cavePermission = await accept.CavePermissions.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == cavePermissionId);
            var userPermission = await accept.UserPermissions.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == userPermissionId);

            accept.AccountUsers.Add(new AccountUser
            {
                AccountId = accountB.AccountId,
                UserId = realUserId,
                InvitationAcceptedOn = DateTime.UtcNow,
                InvitationSentOn = invitationSentOn
            });
            cavePermission.User = realUser;
            userPermission.User = realUser;

            await accept.SaveChangesAsync();

            var temporaryMembership = await accept.AccountUsers.IgnoreQueryFilters().SingleAsync(row =>
                row.AccountId == accountB.AccountId && row.UserId == temporaryUserId);
            var temporaryUser = await accept.Users.SingleAsync(user => user.Id == temporaryUserId);
            accept.AccountUsers.Remove(temporaryMembership);
            accept.Users.Remove(temporaryUser);
            await accept.SaveChangesAsync();
        }

        await using var verify = database.CreateDbContext("verify-b", accountB.AccountId);
        Assert.True(await verify.AccountUsers.AnyAsync(row =>
            row.AccountId == accountB.AccountId && row.UserId == realUserId));
        Assert.False(await verify.AccountUsers.AnyAsync(row =>
            row.AccountId == accountB.AccountId && row.UserId == temporaryUserId));
        Assert.False(await verify.Users.AnyAsync(user => user.Id == temporaryUserId));
        Assert.Equal(realUserId,
            await verify.CavePermissions.IgnoreQueryFilters().Where(row => row.Id == cavePermissionId)
                .Select(row => row.UserId).SingleAsync());
        Assert.Equal(realUserId,
            await verify.UserPermissions.IgnoreQueryFilters().Where(row => row.Id == userPermissionId)
                .Select(row => row.UserId).SingleAsync());
    }

    [Fact]
    public async Task MissingInvitationSentTimestampFailsClosedWhenMatchingInvitationIsAmbiguous()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(MissingInvitationSentTimestampFailsClosedWhenMatchingInvitationIsAmbiguous));
        var accountA = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var accountB = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'b');
        const string realUserId = "membera";
        const string temporaryUserId1 = "tempamb001";
        const string temporaryUserId2 = "tempamb002";
        var invitedEmail = $"{realUserId}@integration.test";

        await using (var seedRealUser = database.CreateDbContext(realUserId, accountA.AccountId))
        {
            seedRealUser.AccountUsers.Add(new AccountUser
            { AccountId = accountA.AccountId, UserId = realUserId });
            await seedRealUser.SaveChangesAsync();
        }

        await using (var seedInvitations = database.CreateDbContext("managerb", accountB.AccountId))
        {
            seedInvitations.Users.AddRange(
                new User("First", "Invite", invitedEmail) { Id = temporaryUserId1, IsTemporary = true },
                new User("Second", "Invite", invitedEmail) { Id = temporaryUserId2, IsTemporary = true });
            seedInvitations.AccountUsers.AddRange(
                new AccountUser
                {
                    AccountId = accountB.AccountId,
                    UserId = temporaryUserId1,
                    InvitationCode = "invite011"
                },
                new AccountUser
                {
                    AccountId = accountB.AccountId,
                    UserId = temporaryUserId2,
                    InvitationCode = "invite012"
                });
            await seedInvitations.SaveChangesAsync();
        }

        await using (var claim = database.CreateDbContext(realUserId, accountA.AccountId))
        {
            claim.AccountUsers.Add(new AccountUser
            {
                AccountId = accountB.AccountId,
                UserId = realUserId,
                InvitationAcceptedOn = DateTime.UtcNow,
                InvitationSentOn = null
            });

            await AssertForbiddenAsync(claim);
        }

        await using var verify = database.CreateDbContext("verify-b", accountB.AccountId);
        Assert.False(await verify.AccountUsers.AnyAsync(row =>
            row.AccountId == accountB.AccountId && row.UserId == realUserId));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RegistrationInvitationClaimWorksWithoutAuthenticatedRequestUser(
        bool persistInvitationSentOn)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(RegistrationInvitationClaimWorksWithoutAuthenticatedRequestUser)}-{persistInvitationSentOn}");
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        const string realUserId = "newusr0001";
        const string temporaryUserId = "tempreg001";
        const string cavePermissionId = "cavereg001";
        const string userPermissionId = "userreg001";
        const string cavePermissionKeyId = "permreg001";
        const string userPermissionKeyId = "permreg002";
        DateTime? invitationSentOn = persistInvitationSentOn ? DateTime.UtcNow.AddMinutes(-10) : null;

        // Registration persists the real user before ClaimInvitation runs, while the request is still unauthenticated.
        await using (var registration = database.CreateDbContext("", null))
        {
            registration.Users.Add(new User("New", "Registrant", $"{realUserId}@integration.test")
            {
                Id = realUserId,
                HashedPassword = "hash",
                EmailConfirmationCode = "confirm01"
            });
            await registration.SaveChangesAsync();
        }

        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = cavePermissionKeyId,
            Name = "View",
            Description = "Registration invitation cave permission",
            Key = "register-invite-view",
            PermissionType = "Cave"
        });
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = userPermissionKeyId,
            Name = "Account permission",
            Description = "Registration invitation account permission",
            Key = "register-invite-account",
            PermissionType = "Account"
        });

        await using (var seedInvitation = database.CreateDbContext("managerb", accountB.AccountId))
        {
            seedInvitation.Users.Add(new User("Invited", "Registration", $"{realUserId}@integration.test")
            {
                Id = temporaryUserId,
                IsTemporary = true
            });
            seedInvitation.AccountUsers.Add(new AccountUser
            {
                AccountId = accountB.AccountId,
                UserId = temporaryUserId,
                InvitationCode = "invite005",
                InvitationSentOn = invitationSentOn
            });
            seedInvitation.CavePermissions.Add(new CavePermission
            {
                Id = cavePermissionId,
                AccountId = accountB.AccountId,
                UserId = temporaryUserId,
                PermissionId = cavePermissionKeyId,
                CaveId = accountB.CaveId
            });
            seedInvitation.UserPermissions.Add(new UserPermission
            {
                Id = userPermissionId,
                AccountId = accountB.AccountId,
                UserId = temporaryUserId,
                PermissionId = userPermissionKeyId
            });
            await seedInvitation.SaveChangesAsync();
        }

        await using (var claim = database.CreateDbContext("", null))
        {
            var realUser = await claim.Users.SingleAsync(user => user.Id == realUserId);
            var cavePermission = await claim.CavePermissions.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == cavePermissionId);
            var userPermission = await claim.UserPermissions.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == userPermissionId);

            claim.AccountUsers.Add(new AccountUser
            {
                AccountId = accountB.AccountId,
                UserId = realUserId,
                InvitationAcceptedOn = DateTime.UtcNow,
                InvitationSentOn = invitationSentOn
            });
            cavePermission.User = realUser;
            userPermission.User = realUser;
            await claim.SaveChangesAsync();

            var temporaryMembership = await claim.AccountUsers.IgnoreQueryFilters().SingleAsync(row =>
                row.AccountId == accountB.AccountId && row.UserId == temporaryUserId);
            var temporaryUser = await claim.Users.SingleAsync(user => user.Id == temporaryUserId);
            claim.AccountUsers.Remove(temporaryMembership);
            claim.Users.Remove(temporaryUser);
            await claim.SaveChangesAsync();
        }

        await using var verify = database.CreateDbContext("verify-b", accountB.AccountId);
        Assert.True(await verify.AccountUsers.AnyAsync(row =>
            row.AccountId == accountB.AccountId && row.UserId == realUserId));
        Assert.False(await verify.AccountUsers.AnyAsync(row =>
            row.AccountId == accountB.AccountId && row.UserId == temporaryUserId));
        Assert.Equal(realUserId,
            await verify.CavePermissions.IgnoreQueryFilters().Where(row => row.Id == cavePermissionId)
                .Select(row => row.UserId).SingleAsync());
        Assert.Equal(realUserId,
            await verify.UserPermissions.IgnoreQueryFilters().Where(row => row.Id == userPermissionId)
                .Select(row => row.UserId).SingleAsync());
    }

    [Fact]
    public async Task LoggedInInviteeCanDeclineInvitationWhileAnotherAccountIsSelected()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(LoggedInInviteeCanDeclineInvitationWhileAnotherAccountIsSelected));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        const string realUserId = "membera";
        const string temporaryUserId = "tempdecl01";

        await using (var seedReal = database.CreateDbContext(realUserId, accountA.AccountId))
        {
            seedReal.AccountUsers.Add(new AccountUser
            { AccountId = accountA.AccountId, UserId = realUserId });
            await seedReal.SaveChangesAsync();
        }

        await using (var seedInvitation = database.CreateDbContext("managerb", accountB.AccountId))
        {
            seedInvitation.Users.Add(new User("Invited", "Decline", $"{realUserId}@integration.test")
            {
                Id = temporaryUserId,
                IsTemporary = true
            });
            seedInvitation.AccountUsers.Add(new AccountUser
            {
                AccountId = accountB.AccountId,
                UserId = temporaryUserId,
                InvitationCode = "invite006",
                InvitationSentOn = DateTime.UtcNow.AddMinutes(-5)
            });
            await seedInvitation.SaveChangesAsync();
        }

        await using (var decline = database.CreateDbContext(realUserId, accountA.AccountId))
        {
            var membership = await decline.AccountUsers.IgnoreQueryFilters().SingleAsync(row =>
                row.AccountId == accountB.AccountId && row.UserId == temporaryUserId);
            var temporaryUser = await decline.Users.SingleAsync(user => user.Id == temporaryUserId);
            decline.AccountUsers.Remove(membership);
            decline.Users.Remove(temporaryUser);
            await decline.SaveChangesAsync();
        }

        await using var verify = database.CreateDbContext("verify-b", accountB.AccountId);
        Assert.False(await verify.AccountUsers.AnyAsync(row =>
            row.AccountId == accountB.AccountId && row.UserId == temporaryUserId));
        Assert.False(await verify.Users.AnyAsync(user => user.Id == temporaryUserId));
    }

    [Fact]
    public async Task UnrelatedLoggedInUserCannotDeleteForeignInvitation()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(UnrelatedLoggedInUserCannotDeleteForeignInvitation));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        const string attackerId = "membera";
        const string temporaryUserId = "tempdecl02";

        await using (var seedAttacker = database.CreateDbContext(attackerId, accountA.AccountId))
        {
            seedAttacker.AccountUsers.Add(new AccountUser
            { AccountId = accountA.AccountId, UserId = attackerId });
            await seedAttacker.SaveChangesAsync();
        }

        await using (var seedInvitation = database.CreateDbContext("managerb", accountB.AccountId))
        {
            seedInvitation.Users.Add(new User("Different", "Invitee", "someone-else@integration.test")
            {
                Id = temporaryUserId,
                IsTemporary = true
            });
            seedInvitation.AccountUsers.Add(new AccountUser
            {
                AccountId = accountB.AccountId,
                UserId = temporaryUserId,
                InvitationCode = "invite007",
                InvitationSentOn = DateTime.UtcNow.AddMinutes(-5)
            });
            await seedInvitation.SaveChangesAsync();
        }

        await using (var attack = database.CreateDbContext(attackerId, accountA.AccountId))
        {
            var membership = await attack.AccountUsers.IgnoreQueryFilters().SingleAsync(row =>
                row.AccountId == accountB.AccountId && row.UserId == temporaryUserId);
            attack.AccountUsers.Remove(membership);
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify-b", accountB.AccountId);
        Assert.True(await verify.AccountUsers.AnyAsync(row =>
            row.AccountId == accountB.AccountId && row.UserId == temporaryUserId));
    }

    [Fact]
    public async Task VerifiedInvitationCannotClaimAnotherUsersPermission()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(VerifiedInvitationCannotClaimAnotherUsersPermission));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        const string realUserId = "membera";
        const string temporaryUserId = "tempinv001";
        const string unrelatedUserId = "otherusr01";
        const string permissionId = "permview01";
        const string unrelatedPermissionId = "otherperm1";
        var invitationSentOn = DateTime.UtcNow.AddMinutes(-10);

        await using (var seedReal = database.CreateDbContext(realUserId, accountA.AccountId))
        {
            seedReal.AccountUsers.Add(new AccountUser
            { AccountId = accountA.AccountId, UserId = realUserId });
            await seedReal.SaveChangesAsync();
        }

        await using (database.CreateDbContext(unrelatedUserId, accountB.AccountId)) { }

        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = permissionId,
            Name = "View",
            Description = "Invitation cave permission",
            Key = "invite-view",
            PermissionType = "Cave"
        });

        await using (var seed = database.CreateDbContext("managerb", accountB.AccountId))
        {
            seed.Users.Add(new User("Invited", "User", $"{realUserId}@integration.test")
            {
                Id = temporaryUserId,
                IsTemporary = true
            });
            seed.AccountUsers.Add(new AccountUser
            {
                AccountId = accountB.AccountId,
                UserId = temporaryUserId,
                InvitationCode = "invite004",
                InvitationSentOn = invitationSentOn
            });
            seed.CavePermissions.Add(new CavePermission
            {
                Id = unrelatedPermissionId,
                AccountId = accountB.AccountId,
                UserId = unrelatedUserId,
                PermissionId = permissionId,
                CaveId = accountB.CaveId
            });
            await seed.SaveChangesAsync();
        }

        await using (var attack = database.CreateDbContext(realUserId, accountA.AccountId))
        {
            var realUser = await attack.Users.SingleAsync(user => user.Id == realUserId);
            var unrelatedPermission = await attack.CavePermissions.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == unrelatedPermissionId);
            attack.AccountUsers.Add(new AccountUser
            {
                AccountId = accountB.AccountId,
                UserId = realUserId,
                InvitationAcceptedOn = DateTime.UtcNow,
                InvitationSentOn = invitationSentOn
            });
            unrelatedPermission.User = realUser;

            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify-b", accountB.AccountId);
        Assert.False(await verify.AccountUsers.AnyAsync(row =>
            row.AccountId == accountB.AccountId && row.UserId == realUserId));
        Assert.Equal(unrelatedUserId,
            await verify.CavePermissions.IgnoreQueryFilters().Where(row => row.Id == unrelatedPermissionId)
                .Select(row => row.UserId).SingleAsync());
    }

    [Fact]
    public async Task VerifiedInvitationCannotExpandPermissionDuringTransfer()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(VerifiedInvitationCannotExpandPermissionDuringTransfer));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        const string realUserId = "membera";
        const string temporaryUserId = "tempinv002";
        const string cavePermissionId = "caveperm02";
        const string lowPermissionId = "permlow001";
        const string highPermissionId = "permhigh01";
        var invitationSentOn = DateTime.UtcNow.AddMinutes(-10);

        await using (var seedReal = database.CreateDbContext(realUserId, accountA.AccountId))
        {
            seedReal.AccountUsers.Add(new AccountUser
            { AccountId = accountA.AccountId, UserId = realUserId });
            await seedReal.SaveChangesAsync();
        }

        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = lowPermissionId,
            Name = "View",
            Description = "Invitation low permission",
            Key = "invite-low",
            PermissionType = PermissionType.Cave
        });
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = highPermissionId,
            Name = "Manage",
            Description = "Invitation high permission",
            Key = "invite-high",
            PermissionType = PermissionType.Cave
        });

        await using (var seed = database.CreateDbContext("managerb", accountB.AccountId))
        {
            seed.Users.Add(new User("Invited", "User", $"{realUserId}@integration.test")
            {
                Id = temporaryUserId,
                IsTemporary = true
            });
            seed.AccountUsers.Add(new AccountUser
            {
                AccountId = accountB.AccountId,
                UserId = temporaryUserId,
                InvitationCode = "invite008",
                InvitationSentOn = invitationSentOn
            });
            seed.CavePermissions.Add(new CavePermission
            {
                Id = cavePermissionId,
                AccountId = accountB.AccountId,
                UserId = temporaryUserId,
                PermissionId = lowPermissionId,
                CaveId = accountB.CaveId
            });
            await seed.SaveChangesAsync();
        }

        await using (var attack = database.CreateDbContext(realUserId, accountA.AccountId))
        {
            var realUser = await attack.Users.SingleAsync(user => user.Id == realUserId);
            var permission = await attack.CavePermissions.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == cavePermissionId);
            attack.AccountUsers.Add(new AccountUser
            {
                AccountId = accountB.AccountId,
                UserId = realUserId,
                InvitationAcceptedOn = DateTime.UtcNow,
                InvitationSentOn = invitationSentOn
            });
            permission.User = realUser;
            permission.PermissionId = highPermissionId;

            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify-b", accountB.AccountId);
        Assert.False(await verify.AccountUsers.AnyAsync(row =>
            row.AccountId == accountB.AccountId && row.UserId == realUserId));
        var persisted = await verify.CavePermissions.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == cavePermissionId);
        Assert.Equal(temporaryUserId, persisted.UserId);
        Assert.Equal(lowPermissionId, persisted.PermissionId);
    }

    [Fact]
    public async Task ForeignTemporaryInviteeCannotBeModifiedMerelyBecauseItIsTemporary()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ForeignTemporaryInviteeCannotBeModifiedMerelyBecauseItIsTemporary));
        var accountA = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var accountB = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'b');
        const string temporaryUserId = "tempinv003";

        await using (var seed = database.CreateDbContext("managerb", accountB.AccountId))
        {
            seed.Users.Add(new User("Foreign", "Invitee", "foreign-temp@example.test")
            {
                Id = temporaryUserId,
                IsTemporary = true
            });
            seed.AccountUsers.Add(new AccountUser
            {
                AccountId = accountB.AccountId,
                UserId = temporaryUserId,
                InvitationCode = "invite009",
                InvitationSentOn = DateTime.UtcNow.AddMinutes(-5)
            });
            await seed.SaveChangesAsync();
        }

        await using (var attack = database.CreateDbContext("membera", accountA.AccountId))
        {
            var temporaryUser = await attack.Users.IgnoreQueryFilters()
                .SingleAsync(user => user.Id == temporaryUserId);
            temporaryUser.FirstName = "Hijacked";
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify-b", accountB.AccountId);
        Assert.Equal("Foreign",
            await verify.Users.Where(user => user.Id == temporaryUserId)
                .Select(user => user.FirstName).SingleAsync());
    }

    [Fact]
    public async Task InvitationMembershipCannotBeModifiedWithoutSelectedAccount()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(InvitationMembershipCannotBeModifiedWithoutSelectedAccount));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        const string temporaryUserId = "tempusr002";

        await using (var seed = database.CreateDbContext("manager", account.AccountId))
        {
            seed.Users.Add(new User("Temp", "Invite", "temp-modify@example.test")
            {
                Id = temporaryUserId,
                IsTemporary = true
            });
            seed.AccountUsers.Add(new AccountUser
            {
                AccountId = account.AccountId,
                UserId = temporaryUserId,
                InvitationCode = "invite002",
                InvitationSentOn = DateTime.UtcNow.AddMinutes(-5)
            });
            await seed.SaveChangesAsync();
        }

        await using (var attack = database.CreateDbContext("", null))
        {
            var membership = await attack.AccountUsers.IgnoreQueryFilters().SingleAsync(row =>
                row.AccountId == account.AccountId && row.UserId == temporaryUserId);
            membership.InvitationSentOn = DateTime.UtcNow;
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        var persisted = await verify.AccountUsers.SingleAsync(row =>
            row.AccountId == account.AccountId && row.UserId == temporaryUserId);
        Assert.NotNull(persisted.InvitationCode);
        Assert.True(persisted.InvitationSentOn < DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task InvitationCleanupWithoutSelectedAccountStillDeletesTemporaryMembershipAndUser()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(InvitationCleanupWithoutSelectedAccountStillDeletesTemporaryMembershipAndUser));
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

        await using (var cleanup = database.CreateDbContext("", null))
        {
            var membership = await cleanup.AccountUsers.SingleAsync(row =>
                row.AccountId == account.AccountId && row.UserId == temporaryUserId);
            var user = await cleanup.Users.SingleAsync(row => row.Id == temporaryUserId);
            cleanup.AccountUsers.Remove(membership);
            cleanup.Users.Remove(user);
            await cleanup.SaveChangesAsync();
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.False(await verify.AccountUsers.AnyAsync(row => row.UserId == temporaryUserId));
        Assert.False(await verify.Users.AnyAsync(row => row.Id == temporaryUserId));
    }

    private static async Task AssertForbiddenAsync(Microsoft.EntityFrameworkCore.DbContext db)
    {
        var exception = await Assert.ThrowsAsync<ApiException>(() => db.SaveChangesAsync());
        Assert.Equal(403, exception.StatusCode);
    }
}
