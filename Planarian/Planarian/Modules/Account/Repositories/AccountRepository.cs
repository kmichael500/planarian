using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Modules.Account.Archive.Models;
using Planarian.Modules.Account.Model;
using Planarian.Shared.Base;

namespace Planarian.Modules.Account.Repositories;

public sealed record AccountFileObjectAddress(string Partition, string Key);

public class AccountRepository<TDbContext> : RepositoryBase<TDbContext> where TDbContext : PlanarianDbContextBase
{
    public AccountRepository(TDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task ExecuteResetAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var fileObjects = await GetFileObjectAddressesForResetAsync(cancellationToken);
            var existing = await DbContext.StorageCleanupTargets.AsNoTracking()
                .Where(row => row.AccountId == RequestUser.AccountId &&
                              row.Purpose == StorageCleanupPurpose.AccountReset)
                .Select(row => new { row.StoragePartition, row.StorageKey, row.IsContainer })
                .ToListAsync(cancellationToken);
            var known = existing.Select(row => (row.StoragePartition, row.StorageKey, row.IsContainer))
                .ToHashSet();
            foreach (var item in fileObjects)
            {
                // Account-container bytes are removed together in one container operation.
                if (string.Equals(item.Partition, RequestUser.AccountContainerName,
                        StringComparison.OrdinalIgnoreCase)) continue;
                if (known.Add((item.Partition, item.Key, false)))
                    DbContext.StorageCleanupTargets.Add(new StorageCleanupTarget
                    {
                        AccountId = RequestUser.AccountId!,
                        StoragePartition = item.Partition, StorageKey = item.Key,
                        Purpose = StorageCleanupPurpose.AccountReset
                    });
            }

            var container = RequestUser.AccountContainerName;
            if (known.Add((container, string.Empty, true)))
                DbContext.StorageCleanupTargets.Add(new StorageCleanupTarget
                {
                    AccountId = RequestUser.AccountId!, StoragePartition = container,
                    StorageKey = string.Empty, IsContainer = true, Purpose = StorageCleanupPurpose.AccountReset
                });

            // Cleanup addresses are committed atomically with the relational purge.
            await DbContext.SaveChangesAsync(cancellationToken);
            await DeleteAllCavePermissions(cancellationToken);
            await DeleteCaveWithRelatedData(cancellationToken);
            await DeleteAllTagTypes(cancellationToken);
            await DeleteAllCounties(cancellationToken);
            await DeleteAllAccountStates(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<List<StorageCleanupTarget>> GetCleanupTargetsAsync(CancellationToken cancellationToken)
        => DbContext.StorageCleanupTargets.AsNoTracking()
            .Where(row => row.AccountId == RequestUser.AccountId &&
                          row.Purpose == StorageCleanupPurpose.AccountReset)
            .OrderBy(row => row.IsContainer).ThenBy(row => row.Id)
            .ToListAsync(cancellationToken);

    public Task<bool> HasPendingCleanupAsync(CancellationToken cancellationToken)
        => DbContext.StorageCleanupTargets.AsNoTracking()
            .AnyAsync(row => row.AccountId == RequestUser.AccountId &&
                             row.Purpose == StorageCleanupPurpose.AccountReset, cancellationToken);

    public Task<int> CompleteCleanupTargetsAsync(IEnumerable<string> ids, CancellationToken cancellationToken)
    {
        var targetIds = ids.ToArray();
        return DbContext.StorageCleanupTargets
            .Where(row => row.AccountId == RequestUser.AccountId &&
                          row.Purpose == StorageCleanupPurpose.AccountReset && targetIds.Contains(row.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Two bounded reference queries per key chunk, rather than two per stored object.</summary>
    public async Task<HashSet<string>> GetConflictedCleanupTargetIdsAsync(
        IReadOnlyList<StorageCleanupTarget> targets, CancellationToken cancellationToken)
    {
        var conflicts = new HashSet<string>();
        foreach (var partition in targets.Where(t => !t.IsContainer)
                     .GroupBy(t => t.StoragePartition, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var chunk in partition.Chunk(500))
            {
                var keys = chunk.Select(t => t.StorageKey).Distinct().ToArray();
                var foreignFiles = await DbContext.Files.IgnoreQueryFilters()
                    .Where(row => row.AccountId != RequestUser.AccountId &&
                                  row.BlobContainer != null && row.BlobContainer.ToLower() == partition.Key.ToLower() && row.BlobKey != null &&
                                  keys.Contains(row.BlobKey))
                    .Select(row => row.BlobKey!)
                    .ToListAsync(cancellationToken);
                var referenced = foreignFiles.ToHashSet();
                foreach (var target in chunk)
                    if (referenced.Contains(target.StorageKey)) conflicts.Add(target.Id);
            }
        }

        return conflicts;
    }

    public async Task<bool> IsContainerReferencedByAnotherAccountAsync(
        string container, CancellationToken cancellationToken)
        => await DbContext.Files.IgnoreQueryFilters()
            .AnyAsync(row => row.AccountId != RequestUser.AccountId &&
                             row.BlobContainer != null && row.BlobContainer.ToLower() == container.ToLower(),
                cancellationToken);

    private async Task<IReadOnlyList<AccountFileObjectAddress>> GetFileObjectAddressesForResetAsync(
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;

        var liveLocators = await DbContext.Files.IgnoreQueryFilters().AsNoTracking()
            .Where(file => file.AccountId == RequestUser.AccountId)
            .Select(file => new { file.BlobContainer, file.BlobKey })
            .ToListAsync(cancellationToken);
        if (liveLocators.Any(file =>
                string.IsNullOrWhiteSpace(file.BlobContainer) !=
                string.IsNullOrWhiteSpace(file.BlobKey)))
            throw ApiExceptionDictionary.Conflict(
                "Cannot safely reset an account with a File that has an incomplete object-storage address.");

        var live = liveLocators
            .Where(file => !string.IsNullOrWhiteSpace(file.BlobContainer))
            .Select(file => new AccountFileObjectAddress(file.BlobContainer!, file.BlobKey!))
            .ToList();
        var pendingFileCleanup = await DbContext.StorageCleanupTargets.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.AccountId == RequestUser.AccountId &&
                          (row.Purpose == StorageCleanupPurpose.CaveAssetRemoval ||
                           row.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval) &&
                          !row.IsContainer)
            .Select(row => new AccountFileObjectAddress(row.StoragePartition, row.StorageKey))
            .ToListAsync(cancellationToken);

        var addresses = live.Concat(pendingFileCleanup).Distinct().ToList();

        // The whole account container includes untracked archives/imports. Even a
        // foreign reference to a key absent from our File table makes deleting
        // that container unsafe.
        if (await IsContainerReferencedByAnotherAccountAsync(RequestUser.AccountContainerName, cancellationToken))
            throw ApiExceptionDictionary.Conflict(
                "Cannot safely reset an account whose storage container is referenced by another account.");

        // Legacy File rows can contain storage locators that are not tied to the
        // account's container. Never delete another tenant's account container
        // or an object still referenced by another account.
        var ownContainer = RequestUser.AccountContainerName;
        foreach (var address in addresses)
        {
            if (address.Partition.StartsWith("account-", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(address.Partition, ownContainer, StringComparison.OrdinalIgnoreCase))
                throw ApiExceptionDictionary.Conflict(
                    "Cannot safely reset an account with a File in another account's storage container.");
        }

        foreach (var partition in addresses.GroupBy(address => address.Partition, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var chunk in partition.Select(address => address.Key).Distinct().Chunk(500))
            {
                var keys = chunk;
                var referencedByAnotherFile = await DbContext.Files.IgnoreQueryFilters()
                    .AnyAsync(file => file.AccountId != RequestUser.AccountId &&
                                      file.BlobContainer != null && file.BlobContainer.ToLower() == partition.Key.ToLower() && file.BlobKey != null &&
                                      keys.Contains(file.BlobKey), cancellationToken);
                if (referencedByAnotherFile)
                    throw ApiExceptionDictionary.Conflict(
                        "Cannot safely reset an account while its stored Files are also referenced by another account.");
            }
        }

        return addresses;
    }

    private async Task DeleteCaveWithRelatedData(CancellationToken cancellationToken)
    {
        const int batchSize = 500;

        // Revision/workflow rows deliberately use restrictive relationships so
        // accepted history survives an ordinary Cave delete.  An account purge
        // is the explicit exception: sever nullable workflow pointers first,
        // then remove the dependent graph in an order that cannot cross the
        // trusted account boundary.
        await DeleteRevisionWorkflowForAccountAsync(cancellationToken);

        int deletedCount = 0;

        // Step 1: Delete entrance-related tags

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.EntranceStatusTags
                .Where(tag => tag.Entrance.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);

        deletedCount = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.EntranceHydrologyTags
                .Where(tag => tag.Entrance.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);
        deletedCount = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.FieldIndicationTags
                .Where(tag => tag.Entrance.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);
        deletedCount = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.EntranceReportedByNameTags
                .Where(tag => tag.Entrance.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);
        deletedCount = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.EntranceOtherTag
                .Where(tag => tag.Entrance.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);

        // Step 2: Delete entrances
        deletedCount = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.Entrances
                .Where(e => e.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);

        // Step 3: Delete cave-related tags
        deletedCount = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.GeologyTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);
        deletedCount = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.StorageCleanupTargets
                .Where(row => row.AccountId == RequestUser.AccountId &&
                              (row.Purpose == StorageCleanupPurpose.CaveAssetRemoval ||
                               row.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval))
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);
        deletedCount = 0;

        do
        {

            deletedCount = await DeleteBatchAsync(DbContext.Files
                .Where(file => file.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);
        deletedCount = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.MapStatusTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);
        deletedCount = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.GeologicAgeTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);
        deletedCount = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.PhysiographicProvinceTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);
        deletedCount = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.BiologyTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);
        deletedCount = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.ArcheologyTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);
        deletedCount = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.CartographerNameTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);
        deletedCount = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.CaveReportedByNameTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);
        deletedCount = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.CaveOtherTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);

        // Favorites and line plots have NO ACTION foreign keys to Cave. They must
        // be removed before their owning Cave, including in a reset with no other tags.
        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.Favorites
                // Legacy rows may have a null AccountId despite their Cave FK.
                // Do not delete an explicitly other-tenant-owned Favorite.
                .Where(favorite => favorite.AccountId == RequestUser.AccountId ||
                                   (favorite.AccountId == null &&
                                    favorite.Cave.AccountId == RequestUser.AccountId))
                .IgnoreQueryFilters(), batchSize, cancellationToken);
        } while (deletedCount == batchSize);

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.CaveGeoJsons
                .Where(plot => plot.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);
        } while (deletedCount == batchSize);

        // Step 4: Delete the cave

        deletedCount = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.Caves
                .Where(cave => cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

        } while (deletedCount == batchSize);

    }

    private async Task DeleteRevisionWorkflowForAccountAsync(CancellationToken cancellationToken)
    {
        var accountId = RequestUser.AccountId
            ?? throw new InvalidOperationException("Account scope is required for cave data deletion.");

        await DbContext.Caves.IgnoreQueryFilters()
            .Where(c => c.AccountId == accountId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CurrentRevisionId, (string?)null), cancellationToken);

        await DbContext.CaveChangeRequests.IgnoreQueryFilters()
            .Where(r => r.AccountId == accountId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.CurrentProposalVersionId, (string?)null)
                .SetProperty(r => r.BaseRevisionId, (string?)null)
                .SetProperty(r => r.ApprovedRevisionId, (string?)null), cancellationToken);

        await DbContext.CaveRevisions.IgnoreQueryFilters()
            .Where(r => r.AccountId == accountId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.PreviousRevisionId, (string?)null)
                .SetProperty(r => r.ChangeRequestId, (string?)null)
                .SetProperty(r => r.ImportBatchId, (string?)null), cancellationToken);

        await DbContext.CaveProposalVersions.IgnoreQueryFilters()
            .Where(v => v.AccountId == accountId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.PreviousProposalVersionId, (string?)null), cancellationToken);

        await DbContext.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
            .Where(link => link.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken);
        await DbContext.CaveChangeRequestReviewEvents.IgnoreQueryFilters()
            .Where(reviewEvent => reviewEvent.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken);
        await DbContext.CaveProposalVersions.IgnoreQueryFilters()
            .Where(v => v.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken);
        await DbContext.CaveRevisions.IgnoreQueryFilters()
            .Where(r => r.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken);
        await DbContext.CaveChangeRequests.IgnoreQueryFilters()
            .Where(r => r.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken);
        await DbContext.CaveImportBatches.IgnoreQueryFilters()
            .Where(b => b.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task DeleteAllTagTypes(CancellationToken cancellationToken)
    {
        const int batchSize = 500; // Adjust based on your needs
        int deletedCount;

        do
        {
            deletedCount = await DbContext.TagTypes
                .Where(e => e.AccountId == RequestUser.AccountId && e.IsDefault == false && !string.IsNullOrWhiteSpace(e.AccountId))
                .Where(tt => DbContext.TagTypes
                    .Where(e => e.AccountId == RequestUser.AccountId)
                    .Take(batchSize)
                    .Select(w => w.Id)
                    .Contains(tt.Id) && tt.AccountId == RequestUser.AccountId)
                .ExecuteDeleteAsync(cancellationToken);

        } while (deletedCount == batchSize); // Continue until fewer than batchSize rows are deleted
    }

    private async Task DeleteAllCounties(CancellationToken cancellationToken)
    {
        await DbContext.Counties.Where(c => c.AccountId == RequestUser.AccountId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    // deletes all cave permissions except view all
    private async Task DeleteAllCavePermissions(CancellationToken cancellationToken)
    {
        await DbContext.CavePermissions.Where(c =>
            c.AccountId == RequestUser.AccountId &&
            !(string.IsNullOrWhiteSpace(c.CaveId) && string.IsNullOrWhiteSpace(c.CountyId)))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task DeleteAllAccountStates(CancellationToken cancellationToken)
    {
        await DbContext.AccountStates.Where(c => c.AccountId == RequestUser.AccountId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetAllStates(CancellationToken cancellationToken)
    {
        return await DbContext.States
            .Select(e => new SelectListItem<string>
            {
                Value = e.Id,
                Display = e.Abbreviation
            })
            .OrderBy(e => e.Display).ToListAsync(cancellationToken);
    }

    public async Task<MiscAccountSettingsVm?> GetMiscAccountSettingsVm(CancellationToken cancellationToken)
    {
        var account = await DbContext.Accounts
            .Where(e => e.Id == RequestUser.AccountId)
            .Select(e => new MiscAccountSettingsVm
            {
                AccountName = e.Name,
                CountyIdDelimiter = e.CountyIdDelimiter,
                StateIds = e.AccountStates.Select(ee => ee.StateId),
                DefaultViewAccessAllCaves = e.DefaultViewAccessAllCaves,
                ExportEnabled = e.ExportEnabled
            })
            .FirstOrDefaultAsync(cancellationToken);

        return account;
    }

    public async Task<Planarian.Model.Database.Entities.RidgeWalker.Account?> GetAccount(
        CancellationToken cancellationToken)
    {
        return await DbContext.Accounts
            .Include(e => e.AccountStates)
            .FirstOrDefaultAsync(e => e.Id == RequestUser.AccountId, cancellationToken);
    }

    public async Task<int> GetNumberOfCavesForState(string deletedStateId, CancellationToken cancellationToken)
    {
        return await DbContext.Caves.IgnoreQueryFilters()
            .Where(e => e.StateId == deletedStateId && e.AccountId == RequestUser.AccountId)
            .CountAsync(cancellationToken);
    }

    public async Task<AccountState?> GetAccountState(string accountId, string deletedStateId)
    {
        return await DbContext.AccountStates
            .Where(e => e.AccountId == accountId && e.StateId == deletedStateId)
            .FirstOrDefaultAsync();
    }

    public async Task<string?> GetAccountName(string accountId)
    {
        return await DbContext.Accounts
            .Where(e => e.Id == accountId)
            .Select(e => e.Name)
            .FirstOrDefaultAsync();
    }

    #region Archive

    public async Task<List<ArchiveCaveCsvModel>> GetArchiveCaves(string accountId, CancellationToken cancellationToken)
    {
        return await DbContext.Caves
            .AsNoTracking()
            // Archive exports are full account exports and must not depend on cave visibility filters.
            .IgnoreQueryFilters()
            .Where(e => e.AccountId == accountId)
            .OrderBy(e => e.State.Name)
            .ThenBy(e => e.County.DisplayId)
            .ThenBy(e => e.CountyNumber)
            .ThenBy(e => e.Name)
            .Select(e => new ArchiveCaveCsvModel
            {
                PlanarianId = e.Id,
                CaveName = e.Name,
                AlternateNames = string.Join(", ", e.AlternateNamesList),
                State = e.State.Abbreviation,
                CountyName = e.County.Name,
                CountyCode = e.County.DisplayId,
                CountyIdDelimiter = e.Account.CountyIdDelimiter,
                CountyCaveNumber = e.CountyNumber,
                CaveLengthFt = e.LengthFeet,
                CaveDepthFt = e.DepthFeet,
                MaxPitDepthFt = e.MaxPitDepthFeet,
                NumberOfPits = e.NumberOfPits,
                Narrative = e.Narrative,
                Geology = string.Join(", ", e.GeologyTags
                    .OrderBy(tag => tag.TagType.Name)
                    .Select(tag => tag.TagType.Name)),
                GeologicAges = string.Join(", ", e.GeologicAgeTags
                    .OrderBy(tag => tag.TagType.Name)
                    .Select(tag => tag.TagType.Name)),
                PhysiographicProvinces = string.Join(", ", e.PhysiographicProvinceTags
                    .OrderBy(tag => tag.TagType.Name)
                    .Select(tag => tag.TagType.Name)),
                Archeology = string.Join(", ", e.ArcheologyTags
                    .OrderBy(tag => tag.TagType.Name)
                    .Select(tag => tag.TagType.Name)),
                Biology = string.Join(", ", e.BiologyTags
                    .OrderBy(tag => tag.TagType.Name)
                    .Select(tag => tag.TagType.Name)),
                ReportedOnDate = e.ReportedOn.HasValue ? e.ReportedOn.Value.ToString("yyyy-MM-dd") : null,
                ReportedByNames = string.Join(", ", e.CaveReportedByNameTags
                    .OrderBy(tag => tag.TagType.Name)
                    .Select(tag => tag.TagType.Name)),
                IsArchived = e.IsArchived,
                OtherTags = string.Join(", ", e.CaveOtherTags
                    .OrderBy(tag => tag.TagType.Name)
                    .Select(tag => tag.TagType.Name)),
                MapStatuses = string.Join(", ", e.MapStatusTags
                    .OrderBy(tag => tag.TagType.Name)
                    .Select(tag => tag.TagType.Name)),
                CartographerNames = string.Join(", ", e.CartographerNameTags
                    .OrderBy(tag => tag.TagType.Name)
                    .Select(tag => tag.TagType.Name)),
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ArchiveEntranceByCaveCsvModel>> GetArchiveEntrances(string accountId, CancellationToken cancellationToken)
    {
        return await DbContext.Entrances
            .AsNoTracking()
            // Archive exports are full account exports and must not depend on cave visibility filters.
            .IgnoreQueryFilters()
            .Where(e => e.Cave.AccountId == accountId)
            .OrderBy(e => e.Cave.State.Name)
            .ThenBy(e => e.Cave.County.DisplayId)
            .ThenBy(e => e.Cave.CountyNumber)
            .ThenByDescending(e => e.IsPrimary)
            .ThenBy(e => e.ReportedOn)
            .ThenBy(e => e.Name)
            .Select(entrance => new ArchiveEntranceByCaveCsvModel
            {
                CavePlanarianId = entrance.CaveId,
                CountyCode = entrance.Cave.County.DisplayId,
                CountyCaveNumber = entrance.Cave.CountyNumber.ToString(),
                EntranceName = entrance.Name,
                DecimalLatitude = entrance.Location.Y,
                DecimalLongitude = entrance.Location.X,
                EntranceElevationFt = entrance.Location.Z,
                LocationQuality = entrance.LocationQualityTag.Name,
                EntranceDescription = entrance.Description,
                EntrancePitDepth = entrance.PitDepthFeet,
                EntranceStatuses = string.Join(", ", entrance.EntranceStatusTags
                    .OrderBy(tag => tag.TagType.Name)
                    .Select(tag => tag.TagType.Name)),
                EntranceHydrology = string.Join(", ", entrance.EntranceHydrologyTags
                    .OrderBy(tag => tag.TagType.Name)
                    .Select(tag => tag.TagType.Name)),
                FieldIndication = string.Join(", ", entrance.FieldIndicationTags
                    .OrderBy(tag => tag.TagType.Name)
                    .Select(tag => tag.TagType.Name)),
                ReportedOnDate = entrance.ReportedOn.HasValue
                    ? entrance.ReportedOn.Value.ToString("yyyy-MM-dd")
                    : null,
                ReportedByNames = string.Join(", ", entrance.EntranceReportedByNameTags
                    .OrderBy(tag => tag.TagType.Name)
                    .Select(tag => tag.TagType.Name)),
                EntranceOtherTags = string.Join(", ", entrance.EntranceOtherTags
                    .OrderBy(tag => tag.TagType.Name)
                    .Select(tag => tag.TagType.Name)),
                IsPrimaryEntrance = entrance.IsPrimary
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ArchiveFileByCaveModel>> GetArchiveFiles(string accountId, CancellationToken cancellationToken)
    {
        return await DbContext.Files
            .AsNoTracking()
            // Archive exports are full account exports and must not depend on cave visibility filters.
            .IgnoreQueryFilters()
            .Where(file => file.AccountId == accountId && !string.IsNullOrWhiteSpace(file.CaveId))
            .OrderBy(file => file.Cave.State.Name)
            .ThenBy(file => file.Cave.County.DisplayId)
            .ThenBy(file => file.Cave.CountyNumber)
            .ThenBy(file => file.FileTypeTag.Name)
            .ThenBy(file => file.Name)
            .ThenBy(file => file.Extension)
            .Select(file => new ArchiveFileByCaveModel
            {
                CavePlanarianId = file.CaveId!,
                Id = file.Id,
                FileName = file.Name + file.Extension,
                BlobKey = file.BlobKey,
                FileTypeDisplayName = file.FileTypeTag.Name
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ArchiveGeoJsonByCaveModel>> GetArchiveGeoJsons(string accountId, CancellationToken cancellationToken)
    {
        return await DbContext.CaveGeoJsons
            .AsNoTracking()
            // Archive exports are full account exports and must not depend on cave visibility filters.
            .IgnoreQueryFilters()
            .Where(geoJson => geoJson.Cave.AccountId == accountId)
            .OrderBy(geoJson => geoJson.Cave.State.Name)
            .ThenBy(geoJson => geoJson.Cave.County.DisplayId)
            .ThenBy(geoJson => geoJson.Cave.CountyNumber)
            .ThenBy(geoJson => geoJson.Name)
            .Select(geoJson => new ArchiveGeoJsonByCaveModel
            {
                Id = geoJson.Id,
                CavePlanarianId = geoJson.CaveId,
                Name = geoJson.Name,
                GeoJson = geoJson.GeoJson
            })
            .ToListAsync(cancellationToken);
    }

    #endregion

    public async Task<AccountUser?> GetAccountUser(string userId, string accountId)
    {
        return await DbContext.AccountUsers
            .Where(e => e.UserId == userId && e.AccountId == accountId)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> GetDefaultViewAccess()
    {
        return await DbContext.Accounts.Where(e => e.Id == RequestUser.AccountId)
            .Select(e => e.DefaultViewAccessAllCaves)
            .FirstOrDefaultAsync();
    }
}

public static class ExpressionExtensions
{
    public static Expression<Func<T, bool>> Compose<T>(this Expression<Func<T, string>> selector,
        Expression<Func<string, bool>> condition)
    {
        var parameter = selector.Parameters[0];
        var body = Expression.Invoke(condition, selector.Body);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

public class AccountRepository : AccountRepository<PlanarianDbContext>
{
    public AccountRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }
}
