using Microsoft.AspNetCore.Mvc;
using Master.Models;
using Master.Services;

namespace Master.Controllers
{
    [Route("policy")]
    [ApiController]
    public class ResiliencePolicyController : ControllerBase
    {
        private readonly ResiliencePolicyManager _policyManager;
        private readonly ILogger<ResiliencePolicyController> _logger;

        public ResiliencePolicyController(ResiliencePolicyManager policyManager, ILogger<ResiliencePolicyController> logger)
        {
            _policyManager = policyManager;
            _logger = logger;
        }

        [HttpPost]
        public ActionResult UpdateSecondaryPolicy(HealthCheckPayload payload)
        {
            _policyManager.UpdateSecondaryCircuitBreaker(payload.Name, payload.GetStatus());
            return NoContent();
        }
    }
}
