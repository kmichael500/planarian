using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;

namespace Planarian.Modules.Tags.Repositories;

public static class EligiblePeopleTagLookup
{
    public static async Task<IReadOnlyList<TagNameCandidate>> GetEligibleAsync(PlanarianDbContextBase db,
        string accountId, CancellationToken cancellationToken) =>
        await db.TagTypes.AsNoTracking()
            .Where(tag => tag.Key == TagTypeKeyConstant.People &&
                          (tag.AccountId == accountId || tag.IsDefault))
            .Select(tag => new TagNameCandidate(tag.Id, tag.Name, tag.Key, tag.AccountId, tag.IsDefault))
            .ToListAsync(cancellationToken);
}
