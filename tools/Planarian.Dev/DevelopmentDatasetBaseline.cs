using System.Security.Cryptography;
using System.Text;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;

internal static class DevelopmentDatasetBaseline
{
    public static readonly DateTime CreatedOn = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public const string DefaultPassword = "planarian";
    public const string AccountName = "Planarian Local Development";
    public const string CountyIdDelimiter = "-";
    public const bool DefaultViewAccessAllCaves = true;
    public const bool ExportEnabled = true;
    public const string StateAbbreviation = "TN";
    public const string CountyName = "Demo County";
    public const string CountyDisplayId = "DEMO";

    public static IReadOnlyList<DatasetTagSpec> Tags { get; } =
    [
        new("geology:bangor", "Bangor Limestone", TagTypeKeyConstant.Geology),
        new("geology:monteagle", "Monteagle Limestone", TagTypeKeyConstant.Geology),
        new("people:surveyor", "Dev Surveyor", TagTypeKeyConstant.People),
        new("other:training", "Training Data", TagTypeKeyConstant.CaveOther)
    ];

    public static IReadOnlyList<DatasetUserSpec> Users(DatasetConfig dataset) =>
    [
        new(dataset.AdminUserId, "Local", "Admin", "admin@planarian.local"),
        new(dataset.ManagerUserId, "Local", "Manager", "manager@planarian.local"),
        new(dataset.ViewerUserId, "Local", "Viewer", "viewer@planarian.local")
    ];

    public static string StableId(string logicalName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(logicalName));
        return Convert.ToHexString(hash).ToLowerInvariant()[..10];
    }

    public static string AccountStateId(DatasetConfig dataset) =>
        StableId($"{dataset.Name}:account-state:{StateAbbreviation.ToLowerInvariant()}");

    public static string FeatureId(DatasetConfig dataset, FeatureKey key) =>
        StableId($"{dataset.Name}:feature:{key}");

    public static string AccountUserId(DatasetConfig dataset, string userId) =>
        StableId($"{dataset.Name}:account-user:{userId}");

    public static string AdminPermissionId(DatasetConfig dataset) =>
        StableId($"{dataset.Name}:admin-permission");

    public static string CavePermissionId(DatasetConfig dataset, string userId, string permissionId) =>
        StableId($"{dataset.Name}:cave-permission:{userId}:{permissionId}");
}

internal sealed record DatasetUserSpec(string Id, string FirstName, string LastName, string EmailAddress);

internal sealed record DatasetTagSpec(string Identity, string Name, string Key)
{
    public string Id(DatasetConfig dataset) =>
        DevelopmentDatasetBaseline.StableId($"{dataset.Name}:tag:{Identity}");
}

internal sealed record DatasetConfig(
    string Name,
    string CountyId,
    string AdminUserId,
    string ManagerUserId,
    string ViewerUserId,
    string BlobContainer)
{
    public static DatasetConfig Parse(string value) => value switch
    {
        "generic" => new("generic", "gencounty1", "genadmin01", "genmanage1", "genviewer1", "planarian-generic"),
        "e2e" => new("e2e", "e2ecounty1", "e2eadmin01", "e2emanage1", "e2eviewer1", "planarian-e2e"),
        _ => throw new ArgumentException("Dataset must be 'generic' or 'e2e'.", nameof(value))
    };
}
