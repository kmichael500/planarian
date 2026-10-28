using Planarian.Model.Database.Revisions;
using Planarian.Modules.Caves.Revisions;
using Xunit;

namespace Planarian.Tests;

internal static class ImportParityAssertions
{
    public static void AssertPublishedSnapshotsEqual(
        CavePublishedSnapshotV1 expected,
        CavePublishedSnapshotV1 actual,
        bool normalizeEntranceIds = false,
        bool normalizeFileIds = false)
    {
        var canonicalExpected = CanonicalizeGeneratedIdentity(expected, normalizeEntranceIds, normalizeFileIds);
        var canonicalActual = CanonicalizeGeneratedIdentity(actual, normalizeEntranceIds, normalizeFileIds);
        Assert.Equal(CaveSnapshotJson.Serialize(canonicalExpected), CaveSnapshotJson.Serialize(canonicalActual));
        Assert.True(new CaveRevisionDiffService().IsSemanticEqual(canonicalExpected, canonicalActual));
    }

    /// <summary>
    /// Independent publication paths can allocate different stable IDs for newly created Entrances or Files.
    /// Normalize only identities that the calling test explicitly declares workflow-generated. Reference TagType,
    /// State, County, Cave, and all user-visible values remain exact so parity tests still catch wrong-reference bugs.
    /// </summary>
    private static CavePublishedSnapshotV1 CanonicalizeGeneratedIdentity(
        CavePublishedSnapshotV1 snapshot,
        bool normalizeEntranceIds,
        bool normalizeFileIds)
    {
        var entrances = normalizeEntranceIds
            ? snapshot.Entrances.Select((entrance, index) => entrance with
            {
                Id = $"<entrance-{index}>"
            }).ToList()
            : snapshot.Entrances;

        var files = normalizeFileIds
            ? snapshot.Files.Select((file, index) => file with
            {
                Id = $"<file-{index}>"
            }).ToList()
            : snapshot.Files;

        return snapshot with
        {
            Entrances = entrances,
            Files = files
        };
    }
}
