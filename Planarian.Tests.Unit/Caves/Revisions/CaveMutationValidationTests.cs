using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Library.Exceptions;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Controllers;
using Planarian.Modules.Tags;
using Xunit;

namespace Planarian.Tests.Unit.Caves.Revisions;

public sealed class CaveMutationValidationTests
{
    [Fact]
    public void AlternateNamesAreTrimmedDeduplicatedAndOrdinallySorted()
    {
        Assert.Equal(["Alpha", "Zulu", "alpha"], CaveAlternateNameNormalizer.Normalize(
            [" Zulu ", "Alpha", "", "Alpha", "alpha", "  "]));
    }

    [Fact]
    public void ReorderedOrWhitespaceOnlyAlternateNamesHaveTheSameCanonicalForm()
    {
        Assert.Equal(CaveAlternateNameNormalizer.Normalize(["Zulu", "Alpha"]),
            CaveAlternateNameNormalizer.Normalize([" Alpha ", "Zulu", "Alpha"]));
    }

    [Fact]
    public void ActualAlternateNameAdditionAndRemovalRemainSemanticChanges()
    {
        var original = Snapshot(["Alpha"]);
        var added = original with { AlternateNames = CaveAlternateNameNormalizer.Normalize(["Alpha", "Zulu"]) };
        var removed = original with { AlternateNames = [] };
        var diff = new CaveRevisionDiffService();

        Assert.False(diff.IsSemanticEqual(original, added));
        Assert.False(diff.IsSemanticEqual(original, removed));
    }

    [Fact]
    public void PeopleOnlyResolutionSeparatesExistingReferencesFromNewIntents()
    {
        var resolved = CaveTagReferencePolicy.Resolve(
            [(SnapshotTagRole.Cartographer, new[] { "person-01", " New Person " })],
            [new TagNameCandidate("person-01", "Existing Person", TagTypeKeyConstant.People,
                "account-01", false)], [], "account-01");

        Assert.Equal("person-01", Assert.Single(resolved.Existing).TagTypeId);
        Assert.Equal(new ProposalPeopleTagIntent(SnapshotTagRole.Cartographer, "New Person"),
            Assert.Single(resolved.NewPeople));
    }

    [Theory]
    [InlineData(SnapshotTagRole.Biology, "Cricket")]
    [InlineData(SnapshotTagRole.Archeology, "Burial")]
    public void NonPeopleFreeFormValuesAreRejected(SnapshotTagRole role, string value) =>
        AssertBadRequest(() => CaveTagReferencePolicy.Resolve([(role, new[] { value })], [], [],
            "account-01"), $"The selected tag is not valid for {role}.");

    [Fact]
    public void WrongKeyAndForeignAccountReferencesAreRejected()
    {
        AssertBadRequest(() => CaveTagReferencePolicy.Resolve(
            [(SnapshotTagRole.Biology, new[] { "tag-01" })],
            [new TagNameCandidate("tag-01", "Wrong", TagTypeKeyConstant.Archeology, "account-01", false)],
            [], "account-01"), "The selected tag is not valid for Biology.");
        AssertBadRequest(() => CaveTagReferencePolicy.Resolve(
            [(SnapshotTagRole.Biology, new[] { "tag-02" })],
            [new TagNameCandidate("tag-02", "Foreign", TagTypeKeyConstant.Biology, "account-02", false)],
            [], "account-01"), "The selected tag is not valid for Biology.");
    }

    [Fact]
    public void StructurallyAmbiguousMutationIsRejectedBeforePersistence()
    {
        var values = ValidMutation();
        values.Entrances = [values.Entrances.Single(), new AddEntranceVm
        {
            Id = "entrance01", IsPrimary = false, LocationQualityTagId = "quality-01",
            Latitude = 2, Longitude = 2, ElevationFeet = 2
        }];

        AssertBadRequest(() => CaveMutationValidation.NormalizeAndValidate(values),
            "Entrance IDs must be unique within a Cave mutation.");
    }

    [Fact]
    public void BlankExistingTagIdIsRejectedWhileDuplicateIdsAreCanonicalized()
    {
        var blank = ValidMutation();
        blank.BiologyTagIds = ["biology-1", " "];
        AssertBadRequest(() => CaveMutationValidation.NormalizeAndValidate(blank), "Tag IDs cannot be blank.");

        var duplicate = ValidMutation();
        duplicate.BiologyTagIds = ["biology-1", "biology-1"];
        CaveMutationValidation.NormalizeAndValidate(duplicate);
        Assert.Equal("biology-1", Assert.Single(duplicate.BiologyTagIds));
    }

    [Fact]
    public void EntranceIdsAreTrimmedBeforeDuplicateValidation()
    {
        var values = ValidMutation();
        values.Entrances =
        [
            values.Entrances.Single(),
            new AddEntranceVm
            {
                Id = " entrance01 ", IsPrimary = false, LocationQualityTagId = "quality-01",
                Latitude = 2, Longitude = 2, ElevationFeet = 2
            }
        ];

        AssertBadRequest(() => CaveMutationValidation.NormalizeAndValidate(values),
            "Entrance IDs must be unique within a Cave mutation.");
    }

    [Fact]
    public void FileIdsAreTrimmedBeforeDuplicateValidation()
    {
        var values = ValidMutation();
        values.Files =
        [
            new EditFileMetadataVm { Id = "file0001" },
            new EditFileMetadataVm { Id = " file0001 " }
        ];

        AssertBadRequest(() => CaveMutationValidation.NormalizeAndValidate(values),
            "File IDs must be unique within a Cave mutation.");
    }

    [Fact]
    public void LinePlotPayloadIsCanonicalizedAndTrimmedBeforePersistence()
    {
        var values = ValidMutation();
        values.LinePlots = [new GeoJsonUploadVm
        {
            Id = " line000001 ", Name = " Survey line ",
            GeoJson = "{ \"type\" : \"FeatureCollection\", \"features\" : [ ] }"
        }];

        CaveMutationValidation.NormalizeAndValidate(values);

        var linePlot = Assert.Single(values.LinePlots!);
        Assert.Equal("line000001", linePlot.Id);
        Assert.Equal("Survey line", linePlot.Name);
        Assert.Equal("{\"features\":[],\"type\":\"FeatureCollection\"}", linePlot.GeoJson);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingLinePlotGeoJsonIsRejectedAsBadRequest(string? geoJson)
    {
        var values = ValidMutation();
        values.LinePlots = [new GeoJsonUploadVm { Name = "Survey line", GeoJson = geoJson! }];

        AssertBadRequest(() => CaveMutationValidation.NormalizeAndValidate(values),
            "Line plot GeoJSON is required.");
    }

    [Fact]
    public void NonFeatureCollectionLinePlotIsRejectedAsBadRequest()
    {
        var values = ValidMutation();
        values.LinePlots = [new GeoJsonUploadVm
        {
            Name = "Survey line", GeoJson = "{\"type\":\"Feature\",\"properties\":{}}"
        }];

        AssertBadRequest(() => CaveMutationValidation.NormalizeAndValidate(values),
            "Line plot GeoJSON must be a valid FeatureCollection.");
    }

    [Fact]
    public void OversizedLinePlotIdentityAndNameAreRejectedBeforePersistence()
    {
        var oversizedId = ValidMutation();
        oversizedId.LinePlots = [new GeoJsonUploadVm
        {
            Id = new string('i', PropertyLength.Id + 1), Name = "Survey",
            GeoJson = "{\"type\":\"FeatureCollection\",\"features\":[]}"
        }];
        AssertBadRequest(() => CaveMutationValidation.NormalizeAndValidate(oversizedId),
            $"Line plot IDs cannot exceed {PropertyLength.Id} characters.");

        var oversizedName = ValidMutation();
        oversizedName.LinePlots = [new GeoJsonUploadVm
        {
            Name = new string('n', PropertyLength.Name + 1),
            GeoJson = "{\"type\":\"FeatureCollection\",\"features\":[]}"
        }];
        AssertBadRequest(() => CaveMutationValidation.NormalizeAndValidate(oversizedName),
            $"Line plot names cannot exceed {PropertyLength.Name} characters.");
    }

    [Theory]
    [InlineData(SnapshotTagRole.Cartographer)]
    [InlineData(SnapshotTagRole.CaveReportedBy)]
    [InlineData(SnapshotTagRole.EntranceReportedBy)]
    public void PeopleNameAtMaximumLengthIsAccepted(SnapshotTagRole role)
    {
        var resolved = CaveTagReferencePolicy.Resolve([(role, new[] { new string('p', PropertyLength.Name) })],
            [], [], "account-01");

        Assert.Equal(PropertyLength.Name, Assert.Single(resolved.NewPeople).Name.Length);
    }

    [Fact]
    public void OversizedPeopleNameIsRejectedAsBadRequest()
    {
        AssertBadRequest(() => CaveTagReferencePolicy.Resolve(
                [(SnapshotTagRole.Cartographer, new[] { new string('p', PropertyLength.Name + 1) })], [],
                [], "account-01"),
            $"People names cannot exceed {PropertyLength.Name} characters.");
    }

    [Fact]
    public void PeopleNameCandidatesBindToExistingIdentityAndUnmatchedCaseVariantsCollapse()
    {
        var resolved = CaveTagReferencePolicy.Resolve(
            [(SnapshotTagRole.Cartographer, new[] { "Existing Person", "New Person", "new person" })], [],
            [new TagNameCandidate("people-01", "Existing Person", TagTypeKeyConstant.People,
                "account-01", false)], "account-01");

        Assert.Equal("people-01", Assert.Single(resolved.Existing).TagTypeId);
        Assert.Equal("New Person", Assert.Single(resolved.NewPeople).Name);
    }

    [Fact]
    public void OnlyExplicitV1PeopleRolesAllowCreationIntents()
    {
        var allowed = Enum.GetValues<SnapshotTagRole>()
            .Where(CaveTagReferencePolicy.AllowsNewPeopleIntent).ToList();

        Assert.Equal(
            [SnapshotTagRole.Cartographer, SnapshotTagRole.CaveReportedBy, SnapshotTagRole.EntranceReportedBy],
            allowed);
    }

    private static AddCaveVm ValidMutation() => new()
    {
        Id = "cave-00001", Name = "Cave", StateId = "state-0001", CountyId = "county-001",
        Entrances = [new AddEntranceVm
        {
            Id = "entrance01", IsPrimary = true, LocationQualityTagId = "quality-01",
            Latitude = 1, Longitude = 1, ElevationFeet = 1
        }]
    };

    private static CavePublishedSnapshotV1 Snapshot(IReadOnlyList<string> alternateNames) => new()
    {
        CaveId = "cave", AccountId = "account", Name = "Cave", AlternateNames = alternateNames,
        State = new SnapshotReference("state", "State"), County = new SnapshotReference("county", "County")
    };

    private static void AssertBadRequest(Action action, string message)
    {
        var exception = Assert.Throws<ApiException>(action);
        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(message, exception.Message);
    }
}
