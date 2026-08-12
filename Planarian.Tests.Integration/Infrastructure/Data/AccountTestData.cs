using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Tests.Integration.Infrastructure.Data;
internal static class AccountTestDataFactory
{
    public static async Task<AccountCountyTestData> CreateAccountWithCountyAsync(
        PostgresTestDatabase database,
        char suffix,
        bool addAccountState = false)
    {
        var upper = char.ToUpperInvariant(suffix);
        var data = new AccountCountyTestData(
            $"acct00000{suffix}",
            $"state0000{suffix}",
            $"State {upper}",
            $"{upper}{upper}",
            $"county000{suffix}",
            $"County {upper}",
            $"{upper}01");

        await GlobalStateTestData.AddAsync(database, data.StateId, data.StateName, data.StateAbbreviation);

        await using var db = database.CreateDbContext($"user-{suffix}", data.AccountId);
        db.Accounts.Add(new Account
        {
            Id = data.AccountId,
            Name = $"Account {upper}",
            CountyIdDelimiter = "-",
            ExportEnabled = true
        });
        db.Counties.Add(new County
        {
            Id = data.CountyId,
            AccountId = data.AccountId,
            StateId = data.StateId,
            DisplayId = data.CountyDisplayId,
            Name = data.CountyName
        });
        if (addAccountState)
        {
            db.AccountStates.Add(new AccountState
            {
                Id = $"acctstate{suffix}",
                AccountId = data.AccountId,
                StateId = data.StateId
            });
        }
        await db.SaveChangesAsync();
        return data;
    }

    public static async Task<string> AddAccountStateAsync(PostgresTestDatabase database, AccountCountyTestData tenant)
    {
        var id = $"acctstate{tenant.AccountId[^1]}";
        await using var db = database.CreateDbContext("account-state-seed", tenant.AccountId);
        db.AccountStates.Add(new AccountState { Id = id, AccountId = tenant.AccountId, StateId = tenant.StateId });
        await db.SaveChangesAsync();
        return id;
    }

}
