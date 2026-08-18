using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Tests;
using Xunit;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Tests.Integration.Files;

public sealed class FilePersistenceConstraintIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task FileNameConstraintRejectsInvalidPersistedNameAndExtensionShapes()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(FileNameConstraintRejectsInvalidPersistedNameAndExtensionShapes));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var valid = await FileTestDataFactory.AddFileAsync(database, tenant,
            fileId: "maxfile001", name: new string('n', 996), extension: ".pdf");

        var invalidCases = new (string Name, string Extension)[]
        {
            (new string('n', 997), ".pdf"),
            ("   ", ".pdf"),
            ("\u00A0", ".pdf"),
            ("\u0085", ".pdf"),
            ("\u3000", ".pdf"),
            (".", ".pdf"),
            ("..", ".pdf"),
            ("nested/report", ".pdf"),
            ("nested\\report", ".pdf"),
            ("line\nbreak", ".pdf"),
            ("report", "."),
            ("report", "pdf"),
            ("report", ".p/df"),
            ("report", ".p\\df"),
            ("report", ".p\ndf")
        };

        for (var index = 0; index < invalidCases.Length; index++)
        {
            var (name, extension) = invalidCases[index];
            await using var db = database.CreateDbContext($"invalid-file-{index}", tenant.AccountId);
            db.Files.Add(new File
            {
                Id = $"bad{index:0000000}",
                AccountId = tenant.AccountId,
                FileTypeTagId = valid.FileTypeId,
                Name = name,
                Extension = extension
            });

            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            var postgres = Assert.IsType<PostgresException>(error.InnerException);
            Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
            Assert.Equal("CK_Files_ValidNameAndExtension", postgres.ConstraintName);
        }
    }
}
