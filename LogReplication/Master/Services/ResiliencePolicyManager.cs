using Grpc.Core;
using HealthChecks.UI.Core;
using HealthChecks.UI.Core.Data;
using Master.Extensions;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Wrap;

namespace Master.Services
{
    public class ResiliencePolicyManager
    {
        private readonly IReadOnlyPolicyRegistry<string> _policyRegistry;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly string[] _secondaries;
        private readonly bool _enforceQuorumAppend;
        private readonly ILogger<ResiliencePolicyManager> _logger;

        private int _isReadOnlyMode = 0;

        public bool IsReadOnlyMode
        {
            get { return Interlocked.CompareExchange(ref _isReadOnlyMode, 1, 1) == 1; }
            set
            {
                if (value) Interlocked.CompareExchange(ref _isReadOnlyMode, 1, 0);
                else Interlocked.CompareExchange(ref _isReadOnlyMode, 0, 1);
            }
        }

        public ResiliencePolicyManager(IReadOnlyPolicyRegistry<string> policyRegistry, IServiceScopeFactory serviceScopeFactory, IConfiguration configuration,  ILogger<ResiliencePolicyManager> logger)
        {
            _policyRegistry = policyRegistry;
            _serviceScopeFactory = serviceScopeFactory;
            _secondaries = configuration.GetSection("Secondaries").Get<string[]>();
            _enforceQuorumAppend = configuration.GetSection("EnforceQuorumAppend").Get<bool>();
            _logger = logger;
        }

        public void UpdateSecondaryCircuitBreaker(string secName, UIHealthStatus healthStatus) 
        {
            var secondaryResiliencePolicy = _policyRegistry.Get<AsyncPolicyWrap<StatusCode>>(PollyConstants.GetSecondaryResiliencePolicyKey(secName));
            var circuitBreakerPolicy = secondaryResiliencePolicy.GetPolicy<AsyncCircuitBreakerPolicy<StatusCode>>();
            if (healthStatus != UIHealthStatus.Healthy)
            {
                _logger.LogInformation("Opening circuit breaker for '{secondary}'.", secName);
                circuitBreakerPolicy.Isolate();
            }
            else
            {
                _logger.LogInformation("Reseting circuit breaker for '{secondary}'.", secName);
                circuitBreakerPolicy.Reset();
            }

            ReviseReadOnlyMode(secName, healthStatus);
        }

        private void ReviseReadOnlyMode(string secName, UIHealthStatus healthStatus) 
        {
            if (!_enforceQuorumAppend) 
            {
                _logger.LogDebug("Quorum append is disabled in settings.");
                return;
            }

            using (var scope = _serviceScopeFactory.CreateScope())
            using (var db = scope.ServiceProvider.GetRequiredService<HealthChecksDb>()) 
            {
                var secondariesHealth = db.Executions.ToDictionary(e => e.Name, e => e);
                secondariesHealth[secName].Status = healthStatus;
                var unhealthySecondariesCount = secondariesHealth.Count(e => e.Value.Status != UIHealthStatus.Healthy);
                var quorum = Math.Ceiling((_secondaries.Length + 1) / (double)2);
                if (unhealthySecondariesCount >= quorum)
                    IsReadOnlyMode = true;
                else
                    IsReadOnlyMode = false;

                _logger.LogInformation("Quorum: '{quorum}'. Unheathy secondaries '{count}'. Read only mode is '{status}'.", quorum, unhealthySecondariesCount, IsReadOnlyMode);
            }
        }
    }
}
