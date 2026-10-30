using System.Reflection;
using System.Text.RegularExpressions;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Notifications.Hubs;
using Planarian.Shared.Models;
using Planarian.Shared.Services;
using Xunit;

namespace Planarian.Tests.Unit.Architecture;

public sealed class CrossStackStringContractTests
{
    [Fact]
    public void CaveApiStringUnionsMatchBackendEnums()
    {
        AssertStringUnionMatchesEnum<CaveChangeRequestStatus>(
            "Planarian.Web/src/Modules/Caves/Models/CaveChangeRequestVm.ts", nameof(CaveChangeRequestStatus));
        AssertStringUnionMatchesEnum<CaveProposalCountyNumberMode>(
            "Planarian.Web/src/Modules/Caves/Models/CaveChangeRequestVm.ts", nameof(CaveProposalCountyNumberMode));
        AssertStringUnionMatchesEnum<CaveRevisionSource>(
            "Planarian.Web/src/Modules/Caves/Models/CaveRevisionVm.ts", nameof(CaveRevisionSource));
        AssertStringUnionMatchesEnum<CaveRevisionOperation>(
            "Planarian.Web/src/Modules/Caves/Models/CaveRevisionVm.ts", nameof(CaveRevisionOperation));
        AssertStringUnionMatchesEnum<SnapshotTagRole>(
            "Planarian.Web/src/Modules/Caves/Models/CaveRevisionVm.ts", nameof(SnapshotTagRole));
    }

    [Fact]
    public void ChunkedUploadWireStatusesMatchBackendEnum()
    {
        var expected = Enum.GetNames<ChunkedUploadSessionStatus>()
            .Select(value => value.ToLowerInvariant());
        var actual = ReadTypeScriptStringUnion(
            "Planarian.Web/src/Shared/Components/FileUploader/types.ts", "ChunkedUploadSessionStatus");

        AssertSameValues(expected, actual);
    }

    [Fact]
    public void FrontendPermissionKeysMatchBackendPolicyKeys()
    {
        var expected = typeof(PermissionPolicyKey)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);
        var actual = ReadTypeScriptStringEnum(
            "Planarian.Web/src/Modules/Authentication/Models/PermissionKey.ts", "PermissionKey");

        AssertSameValues(expected, actual);
    }

    [Fact]
    public void FrontendFeatureKeysMatchBackendFeatureKeys()
    {
        var actual = ReadTypeScriptStringEnum(
            "Planarian.Web/src/Modules/Account/Models/FeatureSettingVm.ts", nameof(FeatureKey));

        AssertSameValues(Enum.GetNames<FeatureKey>(), actual);
    }

    [Fact]
    public void SearchAndReviewWireValuesMatchAcrossStacks()
    {
        var queryOperators = typeof(Planarian.Modules.Query.Models.QueryOperator)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);
        AssertSameValues(queryOperators, ReadTypeScriptStringEnum(
            "Planarian.Web/src/Modules/Search/Services/QueryBuilder.ts", "QueryOperator"));

        AssertSameValues(Enum.GetNames<CaveChangeRequestSortBy>(), ReadTypeScriptStringUnion(
            "Planarian.Web/src/Modules/Caves/Models/CaveChangeRequestVm.ts", nameof(CaveChangeRequestSortBy)));
        AssertSameValues(Enum.GetNames<CaveChangeRequestReviewAction>(), ReadTypeScriptStringUnion(
            "Planarian.Web/src/Modules/Caves/Models/CaveChangeRequestVm.ts", nameof(CaveChangeRequestReviewAction)));
        AssertSameValues(Enum.GetNames<CaveProposalAuthoringConflictKind>(), ReadTypeScriptStringUnion(
            "Planarian.Web/src/Modules/Caves/Models/CaveChangeRequestVm.ts", nameof(CaveProposalAuthoringConflictKind)));
        AssertSameValues(Enum.GetNames<CaveChangeRequestDecisionResult>(), ReadTypeScriptStringUnion(
            "Planarian.Web/src/Modules/Caves/Models/CaveChangeRequestVm.ts", nameof(CaveChangeRequestDecisionResult)));
    }

    [Fact]
    public void NotificationHubDispatchNamesMatchAcrossStacks()
    {
        var methods = ReadTypeScriptStringEnum(
            "Planarian.Web/src/Shared/Components/SignalRProgress/NotificationHubContract.ts",
            "NotificationHubMethod");
        AssertSameValues([nameof(NotificationHub.JoinGroup), nameof(NotificationHub.LeaveGroup)], methods);

        var source = System.IO.File.ReadAllText(Path.Combine(FindRepositoryRoot(),
            "Planarian.Web/src/Shared/Components/SignalRProgress/NotificationHubContract.ts"));
        Assert.Contains($"ReceiveNotification: \"{NotificationHubContract.ReceiveNotification}\"", source);
    }

    [Fact]
    public void FrontendTagTypesMatchBackendTagTypeKeys()
    {
        var backendKeys = typeof(TagTypeKeyConstant)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);
        var frontendKeys = ReadTypeScriptStringEnum(
            "Planarian.Web/src/Modules/Tag/Models/TagType.ts", "TagType");

        AssertSameValues(backendKeys, frontendKeys);
    }

    [Fact]
    public void MaintainedPermissionViewUsesOnlyPersistedPermissionIdentifiers()
    {
        var source = System.IO.File.ReadAllText(Path.Combine(FindRepositoryRoot(),
            "Planarian/Planarian.Model/Database/Views/UserCavePermissions.sql"));
        var sqlIdentifiers = Regex.Matches(source, "'(?<value>[A-Za-z][A-Za-z0-9_]*)'::text")
            .Select(match => match.Groups["value"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(sqlIdentifiers);

        var persistedPermissionKeys = typeof(PermissionKey)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);
        var permissionTypes = typeof(PermissionType)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);
        var allowed = persistedPermissionKeys.Concat(permissionTypes).ToHashSet(StringComparer.Ordinal);

        Assert.All(sqlIdentifiers, identifier => Assert.Contains(identifier, allowed));
    }

    [Fact]
    public void SharedFrontendEnumsMatchBackendContracts()
    {
        AssertStringEnumMatchesEnum<ApiExceptionType>(
            "Planarian.Web/src/Shared/Models/ApiErrorResponse.ts", nameof(ApiExceptionType));
        AssertStringEnumMatchesEnum<MessageDeliveryEventType>(
            "Planarian.Web/src/Shared/Models/MessageDeliveryEventType.ts", nameof(MessageDeliveryEventType));
        AssertStringEnumMatchesEnum<MessageDeliveryStatus>(
            "Planarian.Web/src/Shared/Models/MessageDeliveryStatus.ts", nameof(MessageDeliveryStatus));
        AssertStringEnumMatchesEnum<ProgressState>(
            "Planarian.Web/src/Shared/Models/ProgressState.ts", nameof(ProgressState));

        var permissionTypes = typeof(PermissionType)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);
        AssertSameValues(permissionTypes, ReadTypeScriptStringEnum(
            "Planarian.Web/src/Modules/Authentication/Models/PermissionType.ts", nameof(PermissionType)));
    }

    private static void AssertStringEnumMatchesEnum<TEnum>(string relativePath, string enumName)
        where TEnum : struct, Enum
    {
        AssertSameValues(Enum.GetNames<TEnum>(), ReadTypeScriptStringEnum(relativePath, enumName));
    }

    private static void AssertStringUnionMatchesEnum<TEnum>(string relativePath, string typeName)
        where TEnum : struct, Enum
    {
        AssertSameValues(Enum.GetNames<TEnum>(), ReadTypeScriptStringUnion(relativePath, typeName));
    }

    private static IReadOnlyCollection<string> ReadTypeScriptStringUnion(string relativePath, string typeName)
    {
        var source = System.IO.File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
        var match = Regex.Match(source,
            $@"export\s+type\s+{Regex.Escape(typeName)}\s*=\s*(?<body>.*?);",
            RegexOptions.Singleline);
        Assert.True(match.Success, $"TypeScript string union {typeName} was not found in {relativePath}.");

        return Regex.Matches(match.Groups["body"].Value, "\"([^\"]+)\"")
            .Select(value => value.Groups[1].Value)
            .ToArray();
    }

    private static IReadOnlyCollection<string> ReadTypeScriptStringEnum(string relativePath, string enumName)
    {
        var source = System.IO.File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
        var match = Regex.Match(source,
            $@"export\s+enum\s+{Regex.Escape(enumName)}\s*\{{(?<body>.*?)\}}",
            RegexOptions.Singleline);
        Assert.True(match.Success, $"TypeScript string enum {enumName} was not found in {relativePath}.");

        return Regex.Matches(match.Groups["body"].Value, "=\\s*\"([^\"]+)\"")
            .Select(value => value.Groups[1].Value)
            .ToArray();
    }

    private static void AssertSameValues(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        Assert.Equal(
            expected.OrderBy(value => value, StringComparer.Ordinal),
            actual.OrderBy(value => value, StringComparer.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "Planarian", "Planarian.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Unable to locate the Planarian repository root.");
    }
}
