using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;

internal static class DevelopmentDatasetLocator
{
    public static async Task<string?> FindAccountIdAsync(
        PlanarianDbContext db,
        DatasetConfig dataset,
        CancellationToken cancellationToken = default)
    {
        var markerAccountId = await db.Counties
            .AsNoTracking()
            .Where(county => county.Id == dataset.CountyId)
            .Select(county => county.AccountId)
            .SingleOrDefaultAsync(cancellationToken);
        if (markerAccountId != null) return markerAccountId;

        var candidates = await db.Accounts
            .AsNoTracking()
            .Where(account =>
                account.Name == DevelopmentDatasetBaseline.AccountName &&
                account.AccountUsers.Any(row => row.UserId == dataset.AdminUserId))
            .Select(account => account.Id)
            .Take(2)
            .ToListAsync(cancellationToken);
        return candidates.Count switch
        {
            0 => null,
            1 => candidates[0],
            _ => throw new InvalidOperationException(
                $"Development dataset '{dataset.Name}' matches multiple accounts. " +
                "Refusing to choose one implicitly; restore the fixture marker or reset the local dataset.")
        };
    }

    public static async Task<string> RequireAccountIdAsync(
        PlanarianDbContext db,
        DatasetConfig dataset,
        CancellationToken cancellationToken = default) =>
        await FindAccountIdAsync(db, dataset, cancellationToken)
        ?? throw new InvalidOperationException(
            $"Development dataset '{dataset.Name}' account could not be located.");
}
