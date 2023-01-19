using Planarian.Model.Shared;
using Planarian.Modules.Leads.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Leads.Controllers;

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