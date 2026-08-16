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

    public async Task<IReadOnlyList<AccountFileObjectAddress>> GetFileObjectAddressesForResetAsync(
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;

        var live = await DbContext.Files.IgnoreQueryFilters().AsNoTracking()
            .Where(file => file.AccountId == RequestUser.AccountId &&
                           file.BlobContainer != null && file.BlobKey != null)
            .Select(file => new AccountFileObjectAddress(file.BlobContainer!, file.BlobKey!))
            .ToListAsync(cancellationToken);
        var retained = await DbContext.RetainedCaveFileObjects.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.AccountId == RequestUser.AccountId)
            .Select(row => new AccountFileObjectAddress(row.StoragePartition, row.StorageKey))
            .ToListAsync(cancellationToken);

        return live.Concat(retained).Distinct().ToList();
    }

    public async Task DeleteCaveWithRelatedData(IProgress<string> progress, CancellationToken cancellationToken)
    {
        const int batchSize = 500;

        // Revision/workflow rows deliberately use restrictive relationships so
        // accepted history survives an ordinary Cave delete.  An account purge
        // is the explicit exception: sever nullable workflow pointers first,
        // then remove the dependent graph in an order that cannot cross the
        // trusted account boundary.
        await DeleteRevisionWorkflowForAccountAsync(cancellationToken);

        int deletedCount = 0;
        int totalDeleted = 0;

        // Step 1: Delete entrance-related tags
        progress.Report("Deleting entrance-related tags...");

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.EntranceStatusTags
                .Where(tag => tag.Entrance.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} entrance status tags.");
        } while (deletedCount == batchSize);

        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.EntranceHydrologyTags
                .Where(tag => tag.Entrance.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} entrance hydrology tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.FieldIndicationTags
                .Where(tag => tag.Entrance.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} field indication tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.EntranceReportedByNameTags
                .Where(tag => tag.Entrance.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} entrance reported by name tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.EntranceOtherTag
                .Where(tag => tag.Entrance.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} entrance other tags.");
        } while (deletedCount == batchSize);

        // Step 2: Delete entrances
        progress.Report("Deleting entrances...");
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.Entrances
                .Where(e => e.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} entrances.");
        } while (deletedCount == batchSize);

        // Step 3: Delete cave-related tags
        progress.Report("Deleting cave-related tags...");
        deletedCount = 0;
        totalDeleted = 0;

        totalDeleted = 0;
        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.GeologyTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} geology tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.RetainedCaveFileObjects
                .Where(row => row.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} retained cave file objects.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {

            deletedCount = await DeleteBatchAsync(DbContext.Files
                .Where(file => file.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} files.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.MapStatusTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} map status tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.GeologicAgeTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} geologic age tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.PhysiographicProvinceTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} physiographic province tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.BiologyTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} biology tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.ArcheologyTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} archeology tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.CartographerNameTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} cartographer name tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.CaveReportedByNameTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} cave reported by name tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.CaveOtherTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} cave other tags.");
        } while (deletedCount == batchSize);

        // Step 4: Delete the cave
        progress.Report("Deleting the cave...");

        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DeleteBatchAsync(DbContext.Caves
                .Where(cave => cave.AccountId == RequestUser.AccountId)
                .IgnoreQueryFilters(), batchSize, cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} caves.");
        } while (deletedCount == batchSize);

        progress.Report("Deleted all caves.");
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


    public async Task DeleteAllTagTypes(IProgress<string> progress, CancellationToken cancellationToken)
    {
        const int batchSize = 500; // Adjust based on your needs
        // Batch delete for Caves
        int deletedCount;
        var totalDeleted = 0;


        // Batch delete for TagTypes
        var tagTypesCount =
            await DbContext.TagTypes.CountAsync(e => e.AccountId == RequestUser.AccountId && e.IsDefault == false &&
                     !string.IsNullOrWhiteSpace(e.AccountId), cancellationToken);

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

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} of {tagTypesCount} tags.");
        } while (deletedCount == batchSize); // Continue until fewer than batchSize rows are deleted
    }

    public async Task DeleteAllCounties()
    {
        await DbContext.Counties.Where(c => c.AccountId == RequestUser.AccountId).ExecuteDeleteAsync();
    }

    // deletes all cave permissions except view all
    public async Task DeleteAllCavePermissions()
    {
        await DbContext.CavePermissions.Where(c =>
            c.AccountId == RequestUser.AccountId &&
            !(string.IsNullOrWhiteSpace(c.CaveId) && string.IsNullOrWhiteSpace(c.CountyId))).ExecuteDeleteAsync();
    }

    public async Task DeleteAllAccountStates()
    {
        await DbContext.AccountStates.Where(c => c.AccountId == RequestUser.AccountId).ExecuteDeleteAsync();
    }

    public async Task<IEnumerable<AccountState>> GetAllAccountStates()
    {
        return await DbContext.AccountStates.Where(c => c.AccountId == RequestUser.AccountId).ToListAsync();
    }

    public async Task<IEnumerable<TagTypeTableVm>> GetTagsForTable(string key, CancellationToken cancellationToken)
    {
        var result = await DbContext.TagTypes
            .Where(e => e.Key == key)
            .Where(e => e.AccountId == RequestUser.AccountId || e.IsDefault)
            .Select(e => new TagTypeTableVm
            {
                TagTypeId = e.Id,
                Name = e.Name,
                IsUserModifiable = !string.IsNullOrWhiteSpace(e.AccountId) || !e.IsDefault,
                Occurrences = e.TripTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId) +
                              e.LeadTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId) +
                              e.EntranceStatusTags.Count(ee => ee.Entrance.Cave.AccountId == RequestUser.AccountId) +
                              e.EntranceHydrologyTags.Count(ee => ee.Entrance.Cave.AccountId == RequestUser.AccountId) +
                              e.FieldIndicationTags.Count(ee => ee.Entrance.Cave.AccountId == RequestUser.AccountId) +
                              e.EntranceLocationQualitiesTags.Count(ee =>
                                  ee.Cave.AccountId == RequestUser.AccountId) +
                              e.GeologyTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.CaveReportedByNameTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.CartographerNameTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.EntranceReportedByNameTags.Count(ee => ee.Entrance.Cave.AccountId == RequestUser.AccountId) +
                              e.FileTypeTags.Count(ee =>
                                  ee.ExpiresOn == null && ee.Cave.AccountId == RequestUser.AccountId) + // temp files don't count
                              e.MapStatusTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.GeologicAgeTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.PhysiographicProvinceTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.BiologyTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.ArcheologyTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.CaveOtherTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId)
            })
            .OrderBy(e => e.Name).ToListAsync(cancellationToken);

        if (key.Equals(TagTypeKeyConstant.File) || key.Equals(TagTypeKeyConstant.LocationQuality))
            foreach (var tag in result)
                tag.IsUserModifiable = tag is { IsUserModifiable: true, Occurrences: 0 };
        return result;
    }

    public async Task<int> GetNumberOfOccurrences(string tagTypeId)
    {
        var result = await DbContext.TagTypes
            .Where(e => e.Id == tagTypeId)
            .Select(e => e.TripTags.Count +
                         e.LeadTags.Count +
                         e.EntranceStatusTags.Count +
                         e.EntranceHydrologyTags.Count +
                         e.FieldIndicationTags.Count +
                         e.EntranceLocationQualitiesTags.Count +
                         e.GeologyTags.Count +
                         e.CaveReportedByNameTags.Count +
                         e.CartographerNameTags.Count +
                         e.EntranceReportedByNameTags.Count +
                         e.FileTypeTags.Count +
                         e.MapStatusTags.Count +
                         e.GeologicAgeTags.Count +
                         e.PhysiographicProvinceTags.Count +
                         e.BiologyTags.Count +
                         e.ArcheologyTags.Count +
                         e.CaveOtherTags.Count
            ).FirstOrDefaultAsync();
        return result;
    }

    public async Task<IEnumerable<TagTypeTableCountyVm>> GetCountiesForTable(string stateId,
        CancellationToken cancellationToken)
    {
        var result = await DbContext.Counties
            .Where(e => e.AccountId == RequestUser.AccountId && e.StateId == stateId)
            .Select(e => new TagTypeTableCountyVm
            {
                TagTypeId = e.Id,
                CountyDisplayId = e.DisplayId,
                Name = e.Name,
                IsUserModifiable = !e.Caves.Any(),
                Occurrences = e.Caves.Count()

            })
            .OrderBy(e => e.Name).ToListAsync(cancellationToken);

        return result;
    }

    public async Task<County?> GetCounty(string? countyId, CancellationToken cancellationToken)
    {
        return await DbContext.Counties.FirstOrDefaultAsync(e =>
            e.Id == countyId && e.AccountId == RequestUser.AccountId, cancellationToken);
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

    public async Task<bool> IsDuplicateCountyCode(string countyDisplayId, string stateId, string? excludedCountyId,
        CancellationToken cancellationToken)
    {
        return await EntityFrameworkQueryableExtensions.AnyAsync(DbContext.Counties, e =>
                EF.Functions.ILike(e.DisplayId, $"{countyDisplayId}") && e.DisplayId.Length == countyDisplayId.Length &&
                e.StateId == stateId && e.AccountId == RequestUser.AccountId && e.Id != excludedCountyId,
            cancellationToken);
    }

    public async Task<bool> IsCountyReferencedByCave(string countyId, CancellationToken cancellationToken)
    {
        return await DbContext.Caves.IgnoreQueryFilters().AnyAsync(cave =>
            cave.AccountId == RequestUser.AccountId && cave.CountyId == countyId, cancellationToken);
    }

    public async Task<bool> StateExistsAsync(string stateId, CancellationToken cancellationToken)
    {
        return await DbContext.States.AsNoTracking().AnyAsync(state => state.Id == stateId, cancellationToken);
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
        return await DbContext.Caves.Where(e => e.StateId == deletedStateId && e.AccountId == RequestUser.AccountId)
            .CountAsync(cancellationToken);
    }


    public async Task<AccountState?> GetAccountState(string accountId, string deletedStateId)
    {
        return await DbContext.AccountStates
            .Where(e => e.AccountId == accountId && e.StateId == deletedStateId)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<string>> GetCavesBatch(int cavesBatchSize, CancellationToken cancellationToken)
    {
        return await DbContext.Caves
            .Where(e => e.AccountId == RequestUser.AccountId)
            .Take(cavesBatchSize)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCavesCount(CancellationToken cancellationToken)
    {
        return await DbContext.Caves
            .Where(e => e.AccountId == RequestUser.AccountId)
            .CountAsync(cancellationToken);
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
            .ThenBy(file => file.FileName)
            .Select(file => new ArchiveFileByCaveModel
            {
                CavePlanarianId = file.CaveId!,
                Id = file.Id,
                FileName = file.FileName,
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
