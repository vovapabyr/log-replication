using Microsoft.AspNetCore.Mvc;
using System.Net;
using Master.Models;

namespace Master.Controllers
{
    [Route("policy")]
    [ApiController]
    public class ResiliencePolicyController : ControllerBase
    {
        private readonly ILogger<ResiliencePolicyController> _logger;

        public ResiliencePolicyController(ILogger<ResiliencePolicyController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        public ActionResult UpdateSecondaryPolicy(HealthCheckPayload payload)
        {
            // TODO Open circuit breaker policy for secondary.
            _logger.LogInformation("Updating secondary '{secondary}' status to '{status}'", payload.Name, payload.Status);
            return NoContent();
        }
    }
}
