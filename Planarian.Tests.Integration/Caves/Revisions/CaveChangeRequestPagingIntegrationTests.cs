using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Planarian.Model.Shared;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveChangeRequestPagingIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task MineAndReviewPagesAreBoundedAndUseConstantDatabaseCommandCounts()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(MineAndReviewPagesAreBoundedAndUseConstantDatabaseCommandCounts));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantViewAsync(database, tenant, "reviewer");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
            for (var index = 0; index < 5; index++)
                await contributor.ChangeRequests.CreateAsync(tenant.CaveId,
                    PublishableValues(tenant, quality.Id, $"Proposal {index}"), tenant.RevisionId, default);

        var mineCounter = new CommandCounter();
        await using (var mineDb = database.CreateDbContext("contributor", tenant.AccountId, mineCounter))
        {
            await CavePermissions.AuthenticateAsync(mineDb, tenant.AccountId);
            mineCounter.Reset();
            var page = await IntegrationTestServices.For(mineDb).CaveChangeRequests.ListMineAsync(1, 2, default);
            Assert.Equal(5, page.TotalCount);
            Assert.Equal(2, page.Results.Count());
            Assert.Equal(2, mineCounter.Count);
        }

        var reviewCounter = new CommandCounter();
        await using (var reviewDb = database.CreateDbContext("reviewer", tenant.AccountId, reviewCounter))
        {
            await CavePermissions.AuthenticateAsync(reviewDb, tenant.AccountId);
            reviewCounter.Reset();
            var first = await IntegrationTestServices.For(reviewDb).CaveChangeRequests
                .ListForReviewAsync(1, 2, default);
            Assert.Equal(5, first.TotalCount);
            Assert.Equal(2, first.Results.Count());
            Assert.Equal(2, reviewCounter.Count);

            reviewCounter.Reset();
            var last = await IntegrationTestServices.For(reviewDb).CaveChangeRequests
                .ListForReviewAsync(99, 2, default);
            Assert.Equal(3, last.PageNumber);
            Assert.Single(last.Results);
            Assert.Equal(2, reviewCounter.Count);
            Assert.Empty(first.Results.Select(row => row.Id).Intersect(last.Results.Select(row => row.Id)));
        }
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<string> _commands = new();
        public int Count => _commands.Count;
        public void Reset() { while (_commands.TryDequeue(out _)) { } }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            _commands.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
