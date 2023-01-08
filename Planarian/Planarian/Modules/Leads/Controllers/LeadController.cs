using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Leads.Models;
using Planarian.Modules.TripObjectives.Controllers;
using Planarian.Modules.TripObjectives.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Leads.Controllers;

[Route("api/leads")]
public class LeadController : PlanarianControllerBase<LeadService>
{

    public LeadController(RequestUser requestUser, TokenService tokenService, LeadService service) : base(requestUser, tokenService, service)
    {
    }

    [HttpGet("{leadId:length(10)}")]
    public async Task<ActionResult<IEnumerable<LeadVm>>> GetLead(string leadId)
    {
        var leads = await Service.GetLeads(leadId);
        return new JsonResult(leads);
    }
    
    [HttpDelete("{leadId:length(10)}")]
    public async Task<ActionResult> DeleteLead(string leadId)
    {
        await Service.DeleteLead(leadId);
        return new OkResult();
    }

}