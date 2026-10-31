using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Xunit;

namespace Planarian.Tests.Unit.Caves.Revisions;

public class CaveRevisionFoundationTests
{
    [Fact]
    public void ProposalJsonWithoutOptionalFilePresentationMetadataRemainsReadable()
    {
        const string json = """
            {"schemaVersion":1,"caveId":"c","accountId":"a","name":"Cave","stateId":"s","countyId":"co","files":[{"fileId":"f","disposition":"RetainPublished","fileTypeTagId":"report","name":"Survey","extension":".pdf"}]}
            """;

        var file = Assert.Single(CaveProposalJson.Deserialize(json, 1).Files);

        Assert.Equal("Survey", file.Name);
        Assert.Equal(".pdf", file.Extension);
        Assert.Null(file.FileTypeName);
    }

    [Fact]
    public void RoundTripUnchangedSnapshotIsSemanticNoOp()
    {
        var snapshot = new CavePublishedSnapshotV1
        {
            CaveId = "cave-a", AccountId = "account-a", Name = "Example",
            State = new SnapshotReference("state-a", "State", null, "ST"),
            County = new SnapshotReference("county-a", "County", "001"),
            Tags = [new SnapshotTagReference(SnapshotTagRole.Cartographer, "tag-a", "Alice")]
        };

        var roundTrip = CaveSnapshotJson.Deserialize(CaveSnapshotJson.Serialize(snapshot), 1);
        Assert.True(new CaveRevisionDiffService().IsSemanticEqual(snapshot, roundTrip));
    }

    [Fact]
    public void SemanticEqualityRemainsContentBasedWhileProposalCompatibilityRequiresSameIdentity()
    {
        var first = new CavePublishedSnapshotV1
        {
            CaveId = "cave-a", AccountId = "account-a", Name = "Example",
            State = new SnapshotReference("state-a", "State", null, "ST"),
            County = new SnapshotReference("county-a", "County", "001"),
            Tags = [new SnapshotTagReference(SnapshotTagRole.Biology, "tag-a", "Cricket")]
        };
        var sameContentDifferentIdentity = first with
        {
            CaveId = "cave-b",
            AccountId = "account-b"
        };

        var diff = new CaveRevisionDiffService();

        Assert.True(diff.IsSemanticEqual(first, sameContentDifferentIdentity));
        Assert.False(diff.IsProposalCompatible(first, sameContentDifferentIdentity));
    }

    [Fact]
    public void SameTagIdInDifferentRolesIsNotCollapsed()
    {
        var before = new CavePublishedSnapshotV1
        {
            CaveId = "c", AccountId = "a", Name = "n",
            State = new SnapshotReference("s", "S"), County = new SnapshotReference("co", "C"),
            Tags = [new SnapshotTagReference(SnapshotTagRole.Cartographer, "person", "Alice")]
        };
        var after = before with
        {
            Tags = [new SnapshotTagReference(SnapshotTagRole.CaveReportedBy, "person", "Alice")]
        };

        var diff = new CaveRevisionDiffService().Compare(before, after);
        Assert.Single(diff.AddedTags);
        Assert.Single(diff.RemovedTags);
    }

    [Fact]
    public void CaveReportedByPeopleTagAdditionsAndRemovalsRemainSemanticDifferences()
    {
        var before = new CavePublishedSnapshotV1
        {
            CaveId = "c", AccountId = "a", Name = "n",
            State = new SnapshotReference("s", "S"), County = new SnapshotReference("co", "C"),
            Tags = [new SnapshotTagReference(SnapshotTagRole.CaveReportedBy, "alice", "Alice")]
        };
        var after = before with
        {
            Tags = [new SnapshotTagReference(SnapshotTagRole.CaveReportedBy, "bob", "Bob")]
        };

        var diff = new CaveRevisionDiffService().Compare(before, after);

        Assert.Equal("Alice", Assert.Single(diff.RemovedTags).NameAtRevision);
        Assert.Equal("Bob", Assert.Single(diff.AddedTags).NameAtRevision);
    }

    [Fact]
    public void EquivalentEntranceTagsRemainEqualAfterRoundTrip()
    {
        var snapshot = new CavePublishedSnapshotV1
        {
            CaveId = "c", AccountId = "a", Name = "n",
            State = new SnapshotReference("s", "S"), County = new SnapshotReference("co", "C"),
            Entrances = [new CaveEntranceSnapshotV1
            {
                Id = "entrance", LocationQualityTagId = "quality", LocationQualityNameAtRevision = "Good",
                Tags = [new SnapshotTagReference(SnapshotTagRole.EntranceReportedBy, "person", "Alice")]
            }]
        };

        var roundTrip = CaveSnapshotJson.Deserialize(CaveSnapshotJson.Serialize(snapshot), 1);
        Assert.True(new CaveRevisionDiffService().IsSemanticEqual(snapshot, roundTrip));
    }

    [Fact]
    public void SameTagIdentityWithRenamedHistoricalLabelIsReferenceMetadataChange()
    {
        var r1 = new CavePublishedSnapshotV1
        {
            CaveId = "c", AccountId = "a", Name = "n", Narrative = "old",
            State = new SnapshotReference("s", "State"), County = new SnapshotReference("co", "County"),
            Tags = [new SnapshotTagReference(SnapshotTagRole.Geology, "g1", "Limestone")]
        };
        var r2 = r1 with { Narrative = "new", Tags = [new SnapshotTagReference(SnapshotTagRole.Geology, "g1", "Carbonate Limestone")] };

        var diff = new CaveRevisionDiffService().Compare(r1, r2);
        Assert.Equal(("old", "new"), diff.Scalars[nameof(CavePublishedSnapshotV1.Narrative)]);
        Assert.Empty(diff.AddedTags);
        Assert.Empty(diff.RemovedTags);
        Assert.Contains(diff.ReferenceMetadataChanges, change => change.StableId == "g1" &&
            change.PreviousValue == "Limestone" && change.CurrentValue == "Carbonate Limestone");
        Assert.False(new CaveRevisionDiffService().IsSemanticEqual(r1, r2));
    }

    [Fact]
    public void StateCountyEntranceAndFileLabelsAreReferenceMetadataChanges()
    {
        var before = new CavePublishedSnapshotV1
        {
            CaveId = "c", AccountId = "a", Name = "n",
            State = new SnapshotReference("s", "Georgia", null, "GA"),
            County = new SnapshotReference("co", "Marion", "MAR"),
            Entrances = [new CaveEntranceSnapshotV1 { Id = "e", LocationQualityTagId = "q", LocationQualityNameAtRevision = "Exact" }],
            Files = [new CaveFileSnapshotV1 { Id = "f", FileTypeTagId = "map", FileTypeNameAtRevision = "Map", Name = "x" }]
        };
        var after = before with
        {
            State = new SnapshotReference("s", "State of Georgia", null, "GA"),
            County = new SnapshotReference("co", "Marion", "MA"),
            Entrances = [new CaveEntranceSnapshotV1 { Id = "e", LocationQualityTagId = "q", LocationQualityNameAtRevision = "Survey Grade" }],
            Files = [new CaveFileSnapshotV1 { Id = "f", FileTypeTagId = "map", FileTypeNameAtRevision = "Cave Map", Name = "x" }]
        };

        var diff = new CaveRevisionDiffService().Compare(before, after);
        Assert.Empty(diff.Scalars);
        Assert.Contains("e", diff.ChangedEntrances);
        Assert.Contains("f", diff.ChangedFiles);
        Assert.Equal(4, diff.ReferenceMetadataChanges.Count);
        Assert.True(new CaveRevisionDiffService().IsProposalCompatible(before, after));
        Assert.False(new CaveRevisionDiffService().IsSemanticEqual(before, after));
    }

    [Fact]
    public void ProposalCompatibilityAllowsOnlyReferenceMetadataChanges()
    {
        var before = new CavePublishedSnapshotV1
        {
            CaveId = "c", AccountId = "a", Name = "Cave",
            State = new SnapshotReference("s", "Tennessee", null, "TN"),
            County = new SnapshotReference("co", "Marion", "MAR"),
            Tags = [new SnapshotTagReference(SnapshotTagRole.Biology, "bio", "Cricket")],
            Entrances =
            [
                new CaveEntranceSnapshotV1
                {
                    Id = "e", Name = "Entrance", Latitude = 35, Longitude = -86,
                    LocationQualityTagId = "quality", LocationQualityNameAtRevision = "Survey Grade",
                    Tags = [new SnapshotTagReference(SnapshotTagRole.EntranceHydrology, "hydro", "Wet")]
                }
            ],
            Files =
            [
                new CaveFileSnapshotV1
                {
                    Id = "f", FileTypeTagId = "map", FileTypeNameAtRevision = "Map",
                    Name = "survey", Extension = ".pdf"
                }
            ],
            LinePlots = [new CaveLinePlotSnapshotV1 { Id = "line", Name = "Line", ContentHash = "hash" }]
        };

        var metadataOnly = before with
        {
            State = new SnapshotReference("s", "State of Tennessee", null, "TNN"),
            County = new SnapshotReference("co", "Marion County", "MC"),
            Tags = [new SnapshotTagReference(SnapshotTagRole.Biology, "bio", "Cave Cricket")],
            Entrances =
            [
                before.Entrances.Single() with
                {
                    LocationQualityNameAtRevision = "Grade 1",
                    Tags = [new SnapshotTagReference(SnapshotTagRole.EntranceHydrology, "hydro", "Seasonally Wet")]
                }
            ],
            Files = [before.Files.Single() with { FileTypeNameAtRevision = "Survey Map" }]
        };

        var diff = new CaveRevisionDiffService();
        Assert.True(diff.IsProposalCompatible(before, metadataOnly));

        Assert.False(diff.IsProposalCompatible(before, before with { Name = "Changed" }));
        Assert.False(diff.IsProposalCompatible(before,
            before with { State = new SnapshotReference("s2", "Tennessee", null, "TN") }));
        Assert.False(diff.IsProposalCompatible(before,
            before with { County = new SnapshotReference("co2", "Marion", "MAR") }));
        Assert.False(diff.IsProposalCompatible(before, before with { Tags = [] }));
        Assert.False(diff.IsProposalCompatible(before, before with
        {
            Entrances = [before.Entrances.Single() with { Latitude = 36 }]
        }));
        Assert.False(diff.IsProposalCompatible(before, before with
        {
            Files = [before.Files.Single() with { Name = "renamed" }]
        }));
        Assert.False(diff.IsProposalCompatible(before, before with
        {
            LinePlots = [before.LinePlots.Single() with { ContentHash = "different" }]
        }));
    }

    [Fact]
    public void ProposalCompatibilityRejectsEveryCurrentSubstantiveSnapshotMutation()
    {
        var before = new CavePublishedSnapshotV1
        {
            CaveId = "c", AccountId = "a", Name = "Cave", AlternateNames = ["Old Name"],
            State = new SnapshotReference("s", "Tennessee", null, "TN"),
            County = new SnapshotReference("co", "Marion", "MAR"),
            CountyNumber = 7, LengthFeet = 100, DepthFeet = 20, MaxPitDepthFeet = 10,
            NumberOfPits = 2, Narrative = "Narrative",
            ReportedOn = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            IsArchived = false,
            Tags = [new SnapshotTagReference(SnapshotTagRole.Biology, "bio", "Cricket")],
            Entrances =
            [
                new CaveEntranceSnapshotV1
                {
                    Id = "e", Name = "Entrance", IsPrimary = true, Description = "Description",
                    Latitude = 35, Longitude = -86, Elevation = 500, Srid = 4326,
                    LocationQualityTagId = "quality", LocationQualityNameAtRevision = "Survey Grade",
                    ReportedOn = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc),
                    PitDepthFeet = 8,
                    Tags = [new SnapshotTagReference(SnapshotTagRole.EntranceHydrology, "hydro", "Wet")]
                }
            ],
            Files =
            [
                new CaveFileSnapshotV1
                {
                    Id = "f", FileTypeTagId = "map", FileTypeNameAtRevision = "Map",
                    Name = "survey", Extension = ".pdf"
                }
            ],
            LinePlots = [new CaveLinePlotSnapshotV1 { Id = "line", Name = "Line", ContentHash = "hash" }]
        };
        var entrance = before.Entrances.Single();
        var file = before.Files.Single();
        var linePlot = before.LinePlots.Single();

        var mutations = new (string Name, CavePublishedSnapshotV1 Snapshot)[]
        {
            ("Cave name", before with { Name = "Changed" }),
            ("Alternate names", before with { AlternateNames = ["Different"] }),
            ("State identity", before with { State = new SnapshotReference("s2", "Tennessee", null, "TN") }),
            ("County identity", before with { County = new SnapshotReference("co2", "Marion", "MAR") }),
            ("County number", before with { CountyNumber = 8 }),
            ("Length", before with { LengthFeet = 101 }),
            ("Depth", before with { DepthFeet = 21 }),
            ("Max pit depth", before with { MaxPitDepthFeet = 11 }),
            ("Number of pits", before with { NumberOfPits = 3 }),
            ("Narrative", before with { Narrative = "Different" }),
            ("Reported on", before with { ReportedOn = before.ReportedOn!.Value.AddDays(1) }),
            ("Archived state", before with { IsArchived = true }),
            ("Cave tag addition", before with
            {
                Tags =
                [
                    before.Tags.Single(),
                    new SnapshotTagReference(SnapshotTagRole.Geology, "geo", "Limestone")
                ]
            }),
            ("Cave tag removal", before with { Tags = [] }),
            ("Cave tag role change", before with
            {
                Tags = [new SnapshotTagReference(SnapshotTagRole.CaveOther, "bio", "Cricket")]
            }),
            ("Entrance addition", before with
            {
                Entrances = [entrance, entrance with { Id = "e2", IsPrimary = false }]
            }),
            ("Entrance removal", before with { Entrances = [] }),
            ("Entrance name", before with { Entrances = [entrance with { Name = "Changed" }] }),
            ("Entrance primary", before with { Entrances = [entrance with { IsPrimary = false }] }),
            ("Entrance description", before with { Entrances = [entrance with { Description = "Changed" }] }),
            ("Entrance latitude", before with { Entrances = [entrance with { Latitude = 36 }] }),
            ("Entrance longitude", before with { Entrances = [entrance with { Longitude = -87 }] }),
            ("Entrance elevation", before with { Entrances = [entrance with { Elevation = 501 }] }),
            ("Entrance SRID", before with { Entrances = [entrance with { Srid = 3857 }] }),
            ("Location quality identity", before with
            {
                Entrances = [entrance with { LocationQualityTagId = "quality2" }]
            }),
            ("Entrance reported on", before with
            {
                Entrances = [entrance with { ReportedOn = entrance.ReportedOn!.Value.AddDays(1) }]
            }),
            ("Entrance pit depth", before with { Entrances = [entrance with { PitDepthFeet = 9 }] }),
            ("Entrance tag addition", before with
            {
                Entrances =
                [
                    entrance with
                    {
                        Tags =
                        [
                            entrance.Tags.Single(),
                            new SnapshotTagReference(SnapshotTagRole.EntranceStatus, "status", "Open")
                        ]
                    }
                ]
            }),
            ("Entrance tag removal", before with { Entrances = [entrance with { Tags = [] }] }),
            ("Entrance tag role change", before with
            {
                Entrances =
                [
                    entrance with
                    {
                        Tags =
                        [
                            new SnapshotTagReference(SnapshotTagRole.FieldIndication, "hydro", "Wet")
                        ]
                    }
                ]
            }),
            ("File addition", before with
            {
                Files =
                [
                    file,
                    new CaveFileSnapshotV1
                    {
                        Id = "f2", FileTypeTagId = "map", FileTypeNameAtRevision = "Map",
                        Name = "second", Extension = ".pdf"
                    }
                ]
            }),
            ("File removal", before with { Files = [] }),
            ("File type identity", before with { Files = [file with { FileTypeTagId = "report" }] }),
            ("File name", before with { Files = [file with { Name = "changed" }] }),
            ("File extension", before with { Files = [file with { Extension = ".jpg" }] }),
            ("Line plot addition", before with
            {
                LinePlots =
                [
                    linePlot,
                    new CaveLinePlotSnapshotV1 { Id = "line2", Name = "Second", ContentHash = "hash2" }
                ]
            }),
            ("Line plot removal", before with { LinePlots = [] }),
            ("Line plot name", before with { LinePlots = [linePlot with { Name = "Changed" }] }),
            ("Line plot content", before with { LinePlots = [linePlot with { ContentHash = "different" }] })
        };

        var diff = new CaveRevisionDiffService();
        foreach (var mutation in mutations)
            Assert.False(diff.IsProposalCompatible(before, mutation.Snapshot), mutation.Name);
    }

    [Fact]
    public void ScopeFailsClosedWithoutAccount()
    {
        using var db = new PlanarianDbContext(new DbContextOptionsBuilder<PlanarianDbContext>().Options);
        var user = new RequestUser(db) { Id = "user-a", AccountId = null };

        Assert.Throws<InvalidOperationException>(() => AccountExecutionScope.Require(user));
    }
}
