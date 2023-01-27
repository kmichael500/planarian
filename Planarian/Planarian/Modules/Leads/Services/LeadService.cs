using Planarian.Model.Database.Entities.Leads;
using Planarian.Model.Shared;
using Planarian.Modules.Leads.Repositories;
using Planarian.Shared.Base;

namespace Planarian.Modules.Leads.Services;

public class LeadService : ServiceBase<LeadRepository>
{
    public LeadService(LeadRepository repository, RequestUser requestUser) : base(repository, requestUser)
    {
    }

    public async Task<LeadVm?> GetLeads(string leadId)
    {
        var lead = await Repository.GetLead(leadId);
        return lead;
    }

    public async Task DeleteLead(string leadId)
    {
        var lead = await Repository.Get(leadId);

        if (lead == null) throw new ArgumentOutOfRangeException(nameof(lead), "Lead not found");
        Repository.Delete(lead);

        await Repository.SaveChangesAsync();
    }
}