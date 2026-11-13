using Microsoft.AspNetCore.Mvc;
using Samasarathi.Application.Contracts;
using Samasarathi.Domain;

namespace Samasarathi.Api.Controllers;

[ApiController]
[Route("api/support")]
public class SupportController(SupportOptions options) : ControllerBase
{
    /// <summary>
    /// Phone, WhatsApp and email of the support team. Open to everyone (a driver who can't log in may need it too);
    /// the details are public by nature and come from the "Support" config section.
    /// </summary>
    [HttpGet]
    public ActionResult<SupportInfo> Get() => SupportInfo.From(options);
}
