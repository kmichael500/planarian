using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Account.Repositories;

public class CaveChangeLogRepository : RepositoryBase
{
    public CaveChangeLogRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext,
        requestUser)
    {
    }

    public async Task<IEnumerable<CaveHistory>> GetCaveHistory(string caveId, CancellationToken cancellationToken)
    {
        var value = await DbContext.CaveChangeRequests
            .Where(e => e.CaveId == caveId && e.AccountId == RequestUser.AccountId && e.IsApproved == true)
            .Select(e => new CaveHistory
            {
                ChangedByUserId = e.CreatedByUserId,
                ApprovedByUserId = e.ReviewedByUserId,
                SubmittedOn = e.CreatedOn,
                ReviewedOn = e.ReviewedOn,
                Records = e.CaveChangeHistory.Select(record => new CaveHistoryRecord
                {
                    CaveId = record.CaveId,
                    EntranceId = record.EntranceId,
                    ChangedByUserId = record.ChangedByUserId,
                    ApprovedByUserId = record.ApprovedByUserId,
                    PropertyName = record.PropertyName,
                    PropertyId = record.PropertyId,
                    ChangeType = record.ChangeType,
                    ChangeValueType = record.ChangeValueType,
                    ValueString = record.ValueString,
                    ValueInt = record.ValueInt,
                    ValueDouble = record.ValueDouble,
                    ValueBool = record.ValueBool,
                    ValueDateTime = record.ValueDateTime,
                    CreatedOn = e.ReviewedOn!.Value,
                })
            })
            .OrderByDescending(e => e.ReviewedOn)
            .ToListAsync(cancellationToken);

        return value;
    }
}

public class CaveHistory
{
    public string? ChangedByUserId { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTime SubmittedOn { get; set; }
    public DateTime? ReviewedOn { get; set; }
    public IEnumerable<HistoryDetail> CaveHistoryDetails { get; set; } = new List<HistoryDetail>();
    public IEnumerable<EntranceHistorySummary> EntranceHistorySummary { get; set; } = new List<EntranceHistorySummary>();
    public IEnumerable<CaveHistoryRecord> Records { get; set; } = new List<CaveHistoryRecord>();
}

public class HistoryDetail
{
    public string PropertyName { get; set; } = null!;
    
    public IEnumerable<string> ValueStrings { get; set; } = new List<string>();
    public string? ValueString { get; set; }
    public int? ValueInt { get; set; }
    public double? ValueDouble { get; set; }
    public bool? ValueBool { get; set; }
    public DateTime? ValueDateTime { get; set; }
    
    public IEnumerable<string> PreviousValueStrings { get; set; } = new List<string>();
    public string? PreviousValueString { get; set; }
    public int? PreviousValueInt { get; set; }
    public double? PreviousValueDouble { get; set; }
    public bool? PreviousValueBool { get; set; }
    public DateTime? PreviousValueDateTime { get; set; }
}

public class EntranceHistorySummary
{
    public string EntranceName { get; set; } = null!;
    public string EntranceId { get; set; } = null!;
    public IEnumerable<HistoryDetail> Details { get; set; } = new List<HistoryDetail>();
    public string ChangeType { get; set; } = null!;
}