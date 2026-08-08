from pathlib import Path


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)

# PostgreSQL timestamptz requires UTC DateTime values. Cave imports already
# normalize parsed dates; Entrance imports must do the same.
path = Path("Planarian/Planarian/Modules/Import/Planning/EntranceImportPlanner.cs")
text = path.read_text()
text = replace_once(
    text,
    "using Planarian.Library.Exceptions;\nusing Planarian.Library.Extensions.String;",
    "using Planarian.Library.Exceptions;\nusing Planarian.Library.Extensions.DateTime;\nusing Planarian.Library.Extensions.String;",
    "Entrance planner DateTime extension using")
text = replace_once(
    text,
    "                var validDate = DateTime.TryParse(record.ReportedOnDate, out var reportedOn);\n",
    "                var validDate = DateTime.TryParse(record.ReportedOnDate, out var reportedOn);\n                reportedOn = reportedOn.ToUtcKind();\n",
    "Entrance planner UTC normalization")
path.write_text(text)

# Two compatibility assertions used fixture identifiers/keys that the model
# intentionally normalizes or treats case-sensitively. Use the real constant
# and a stable 10-character EntityBase id so the test measures import behavior.
path = Path("Planarian.Tests/CaveImportCompatibilityTests.cs")
text = path.read_text()
text = replace_once(text, 'Tag(t.AccountId,"geology","Foo")',
                    'Tag(t.AccountId,TagTypeKeyConstant.Geology,"Foo")',
                    "Cave compatibility geology key")
text = replace_once(text, 'SeedEntrance(d,t,"preserved")',
                    'SeedEntrance(d,t,"preserved0")',
                    "Cave compatibility preserved entrance seed")
text = replace_once(text, 'e.Id=="preserved"', 'e.Id=="preserved0"',
                    "Cave compatibility preserved entrance assertion")
path.write_text(text)

# The benchmark constructs a custom intercepted DbContext rather than using the
# fixture helper, so explicitly seed its audit user before writes begin.
path = Path("Planarian.Tests/ImportScaleBenchmarkTests.cs")
text = path.read_text()
old = 'private static PlanarianDbContext Context(PostgresTestDatabase d,string account,SqlTimingInterceptor sql,SaveMetricsInterceptor saves){var options=new DbContextOptionsBuilder<PlanarianDbContext>()'
new = 'private static PlanarianDbContext Context(PostgresTestDatabase d,string account,SqlTimingInterceptor sql,SaveMetricsInterceptor saves){using(d.CreateDbContext("bench",account)){}var options=new DbContextOptionsBuilder<PlanarianDbContext>()'
text = replace_once(text, old, new, "Scale benchmark persisted audit user")
path.write_text(text)

# Reference-metadata rename coverage needs a historical revision that actually
# contains the stable tag id with the old label. The original seed revision was
# created before the test attached the GeologyTag, so an old->new label change
# could only appear as an added tag. Establish an accepted revision for every
# Cave after the association is attached, then rename the shared tag.
path = Path("Planarian.Tests/ReferenceRenameNoFanOutIntegrationTests.cs")
text = path.read_text()
old = '''        var pointers = new Dictionary<string, string>();
        await using (var db = database.CreateDbContext("manager", tenant.AccountId))
        {
            var reader = new CavePublishedSnapshotReader(db, db.RequestUser);
            foreach (var caveId in caveIds.Skip(1))
            {
                var snapshot = await reader.BuildAsync(caveId);
                var revision = new CaveRevision { Id = IdGenerator.Generate(), AccountId = tenant.AccountId, CaveId = caveId, Source = CaveRevisionSource.ManagerEdit, Operation = CaveRevisionOperation.Create, SnapshotSchemaVersion = 1, SnapshotJson = CaveSnapshotJson.Serialize(snapshot) };
                db.CaveRevisions.Add(revision);
                await db.SaveChangesAsync();
                var cave = await db.Caves.IgnoreQueryFilters().SingleAsync(c => c.Id == caveId);
                cave.CurrentRevisionId = revision.Id;
                await db.SaveChangesAsync();
                pointers[caveId] = revision.Id;
            }
            pointers[tenant.CaveId] = tenant.RevisionId;
        }'''
new = '''        var pointers = new Dictionary<string, string>();
        await using (var db = database.CreateDbContext("manager", tenant.AccountId))
        {
            var reader = new CavePublishedSnapshotReader(db, db.RequestUser);
            foreach (var caveId in caveIds)
            {
                var snapshot = await reader.BuildAsync(caveId);
                var previousRevisionId = caveId == tenant.CaveId ? tenant.RevisionId : null;
                var revision = new CaveRevision
                {
                    Id = IdGenerator.Generate(), AccountId = tenant.AccountId, CaveId = caveId,
                    PreviousRevisionId = previousRevisionId, Source = CaveRevisionSource.ManagerEdit,
                    Operation = CaveRevisionOperation.Update, SnapshotSchemaVersion = 1,
                    SnapshotJson = CaveSnapshotJson.Serialize(snapshot)
                };
                db.CaveRevisions.Add(revision);
                await db.SaveChangesAsync();
                var cave = await db.Caves.IgnoreQueryFilters().SingleAsync(c => c.Id == caveId);
                cave.CurrentRevisionId = revision.Id;
                await db.SaveChangesAsync();
                pointers[caveId] = revision.Id;
            }
        }'''
text = replace_once(text, old, new, "Reference rename historical tagged revisions")
text = replace_once(
    text,
    'result = await coordinator.PublishExistingAsync(tenant.CaveId, tenant.RevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, cave => cave.Name = "Legitimate edit");',
    'result = await coordinator.PublishExistingAsync(tenant.CaveId, pointers[tenant.CaveId], CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, cave => cave.Name = "Legitimate edit");',
    "Reference rename expected pointer")
text = replace_once(
    text,
    'var previous = await verify.CaveRevisions.SingleAsync(r => r.Id == tenant.RevisionId);',
    'var previous = await verify.CaveRevisions.SingleAsync(r => r.Id == pointers[tenant.CaveId]);',
    "Reference rename diff previous revision")
path.write_text(text)
