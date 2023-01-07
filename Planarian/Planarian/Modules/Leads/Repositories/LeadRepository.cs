using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Modules.Leads.Models;
using Planarian.Modules.TripObjectives.Controllers;
using Planarian.Shared.Base;

namespace Planarian.Modules.Leads.Controllers;

public class LeadRepository : RepositoryBase
{
    public LeadRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<LeadVm?> GetLead(string leadId)
    {
        return await DbContext.Lead.Where(e =>
                e.Id == leadId && e.TripObjective.Trip.Project.ProjectMembers.Any(ee => ee.UserId == RequestUser.Id))
            .Select(e => new LeadVm(e))
            .FirstOrDefaultAsync();
    }

    public async Task<Lead?> Get(string leadId)
    {
        return await DbContext.Lead.Where(e =>
                e.Id == leadId && e.TripObjective.Trip.Project.ProjectMembers.Any(ee => ee.UserId == RequestUser.Id))
            .FirstOrDefaultAsync();
    }
}