using Grpc.Core;
using HealthChecks.UI.Core;
using Master.Extensions;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Wrap;

namespace Master.Services
{
    public class ResiliencePolicyManager
    {
        private readonly IReadOnlyPolicyRegistry<string> _policyRegistry;
        private readonly ILogger<ResiliencePolicyManager> _logger;

        public ResiliencePolicyManager(IReadOnlyPolicyRegistry<string> policyRegistry, ILogger<ResiliencePolicyManager> logger)
        {
            _policyRegistry = policyRegistry;
            _logger = logger;
        }

        public void UpdateSecondaryCircuitBreaker(string secName, UIHealthStatus healthStatus) 
        {
            _logger.LogInformation("Updating secondary '{secondary}' status to '{status}'", secName, healthStatus);
            var secondaryResiliencePolicy = _policyRegistry.Get<AsyncPolicyWrap<StatusCode>>(PollyConstants.GetSecondaryResiliencePolicyKey(secName));
            var circuitBreakerPolicy = secondaryResiliencePolicy.GetPolicy<AsyncCircuitBreakerPolicy<StatusCode>>();
            if (healthStatus != UIHealthStatus.Healthy)
            {
                _logger.LogDebug("Opening circuit breaker for '{secondary}'.", secName);
                circuitBreakerPolicy.Isolate();
            }
            else
            {
                _logger.LogDebug("Reseting circuit breaker for '{secondary}'.", secName);
                circuitBreakerPolicy.Reset();
            }
        }
    }
}
