using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Query.Models;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Search;

public class CaveSearchQueryContractTests
{
    [Fact]
    public void SearchDocument_IsPersistedAndUsesSearchIndexes()
    {
        using var context = CreateContext();

        var designTimeModel = context.GetService<IDesignTimeModel>().Model;
        var entityType = designTimeModel.FindEntityType(typeof(CaveSearchDocument));

        Assert.NotNull(entityType);
        Assert.Equal("CaveSearchDocuments", entityType!.GetTableName());
        Assert.Null(entityType.GetViewName());
        Assert.Contains(
            designTimeModel.GetPostgresExtensions(),
            extension => extension.Name == "pg_trgm");
        Assert.NotNull(designTimeModel.FindAnnotation(
            SqlMigrationScriptRegistry.GetAnnotationName("CaveSearchDocument", "Hash")));

        var searchVectorIndex = entityType.GetIndexes()
            .Single(index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(CaveSearchDocument.SearchVector)]));
        Assert.Equal("GIN", searchVectorIndex.GetMethod());

        foreach (var propertyName in new[]
                 {
                     nameof(CaveSearchDocument.DisplayId),
                     nameof(CaveSearchDocument.NameSearchText)
                 })
        {
            var trigramIndex = entityType.GetIndexes()
                .Single(index => index.Properties.Select(property => property.Name)
                    .SequenceEqual([propertyName]));

            Assert.Equal("GIN", trigramIndex.GetMethod());
            Assert.Equal(["gin_trgm_ops"], trigramIndex.GetOperators());
        }
    }

    [Fact]
    public void SearchMaintenanceScript_UsesIncrementalTriggersAndSimpleTextSearch()
    {
        var script = SqlMigrationScriptRegistry.GetCurrentScripts()["CaveSearchDocument"];

        Assert.Contains("RefreshCaveSearchDocument", script.UpSql, StringComparison.Ordinal);
        Assert.Contains("CREATE TRIGGER", script.UpSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("to_tsvector('simple'", script.UpSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("to_tsvector('english'", script.UpSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SELECT \"RefreshCaveSearchDocument\"(NULL)", script.UpSql, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE OR REPLACE VIEW", script.UpSql, StringComparison.OrdinalIgnoreCase);

        var sourceTables = new[]
        {
            "Caves",
            "GeologyTags",
            "GeologicAgeTags",
            "PhysiographicProvinceTags",
            "BiologyTags",
            "ArcheologyTags",
            "MapStatusTags",
            "CartographerNameTags",
            "CaveReportedByNameTags",
            "CaveOtherTags",
            "Entrances",
            "EntranceStatusTags",
            "FieldIndicationTags",
            "EntranceHydrologyTags",
            "EntranceReportedByNameTags",
            "EntranceOtherTag",
            "Files",
            "TagTypes",
            "Counties",
            "States",
            "Accounts"
        };

        foreach (var sourceTable in sourceTables)
        {
            Assert.Contains($"ON \"{sourceTable}\"", script.UpSql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void UnifiedSearchFilter_UsesThePersistedSearchTableOnce()
    {
        using var context = CreateContext();
        var requestUser = new RequestUser(context)
        {
            Id = "test-user",
            AccountId = "test-account"
        };
        var repository = new CaveRepository<PlanarianDbContext>(context, requestUser);
        var filter = new FilterQuery
        {
            Conditions =
            [
                new QueryCondition(
                    nameof(CaveSearchParamsVm.Search),
                    QueryOperator.FreeText,
                    "Bangor pit")
            ]
        };

        var sql = repository.GetCaveIds(filter).ToQueryString();

        Assert.Contains("plainto_tsquery('simple'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ILIKE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"CaveSearchDocuments\"", sql, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(sql, "\"CaveSearchDocuments\" AS"));
    }

    [Fact]
    public void SearchRankingAndHeadline_PrimitivesTranslateThroughNpgsql()
    {
        using var context = CreateContext();
        const string searchText = "Bangor pit";
        const string containsPattern = "%Bangor pit%";
        var normalizedSearch = searchText.ToLowerInvariant();

        var query = context.CaveSearchDocuments
            .Where(document =>
                document.SearchVector.Matches(
                    EF.Functions.PlainToTsQuery("simple", searchText))
                || EF.Functions.ILike(document.DisplayId, containsPattern, "\\")
                || EF.Functions.ILike(document.NameSearchText, containsPattern, "\\"))
            .OrderByDescending(document => document.DisplayId.ToLower() == normalizedSearch)
            .ThenByDescending(document => document.Name.ToLower() == normalizedSearch)
            .ThenByDescending(document => EF.Functions.ILike(
                document.NameSearchText,
                containsPattern,
                "\\"))
            .ThenByDescending(document => document.PhraseVector.Matches(
                EF.Functions.PhraseToTsQuery("simple", searchText)))
            .ThenByDescending(document => document.SearchVector.RankCoverDensity(
                EF.Functions.PlainToTsQuery("simple", searchText)))
            .Select(document => EF.Functions.PlainToTsQuery("simple", searchText)
                .GetResultHeadline(
                    "simple",
                    document.SnippetText,
                    "StartSel=<mark>,StopSel=</mark>,MaxFragments=3"));

        var sql = query.ToQueryString();

        Assert.Contains("ILIKE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("plainto_tsquery('simple'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("phraseto_tsquery('simple'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ts_rank_cd", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ts_headline", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Single(Regex.Matches(sql, "\"CaveSearchDocuments\" AS"));
    }

    private static PlanarianDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PlanarianDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=planarian_search_contract_tests;Username=unused;Password=unused",
                options => options.UseNetTopologySuite())
            .Options;

        return new PlanarianDbContext(options);
    }
}
