using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Shared.Email.Models;
using Planarian.Shared.Email.Services;

namespace Planarian.Shared.Email.Controllers;

[Route("api/webhooks/mailgun")]
public class MailgunWebhookController : ControllerBase
{
    private readonly MailgunWebhookService _service;

    public MailgunWebhookController(MailgunWebhookService service)
    {
        _service = service;
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpPost]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> Handle([FromBody] MailgunWebhookVm? payload,
        CancellationToken cancellationToken)
    {
        var result = await _service.Process(payload, cancellationToken);
        return result switch
        {
            MailgunWebhookProcessingResult.Accepted => Ok(),
            MailgunWebhookProcessingResult.Rejected => StatusCode(StatusCodes.Status406NotAcceptable),
            MailgunWebhookProcessingResult.Retry => StatusCode(StatusCodes.Status503ServiceUnavailable),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable)
        };
    }
}
