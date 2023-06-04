using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Database.Entities.Leads;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Leads.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Leads.Controllers;

[Route("api/leads")]
[AllowAnonymous]
public class LeadController : PlanarianControllerBase<LeadService>
{
    public LeadController(RequestUser requestUser, TokenService tokenService, LeadService service) : base(requestUser,
        tokenService, service)
    {
    }

    [HttpGet("{leadId:length(10)}")]
    public async Task<ActionResult<IEnumerable<LeadVm>>> GetLead(string leadId)
    {
        var leads = await Service.GetLeads(leadId);
        return new JsonResult(leads);
    }

    [HttpGet("")]
    public async Task<ActionResult<IEnumerable<LeadVm>>> GetLeads(
        [FromQuery] IEnumerable<QueryCondition> queryCondition)
    {
        // var leads = await Service.GetLeads(queryCondition);


        return new JsonResult(queryCondition);
    }


    [HttpDelete("{leadId:length(10)}")]
    public async Task<ActionResult> DeleteLead(string leadId)
    {
        await Service.DeleteLead(leadId);
        return new OkResult();
    }
}