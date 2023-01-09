using Grpc.Core;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;

namespace Master.Extensions
{
    internal static class ServiceCollectionExtensions
    {        
        private static StatusCode[] _gRpcErrors = new StatusCode[]
        {
            StatusCode.DeadlineExceeded,
            StatusCode.Internal,
            StatusCode.NotFound,
            StatusCode.ResourceExhausted,
            StatusCode.Unavailable,
            StatusCode.Unknown
        };

        public static IServiceCollection ConfigurePollyPolicies(this IServiceCollection services, IConfiguration configuration)
        {
            var secondaryCallTimeout = configuration.GetSection("SecondaryCallTimeout").Get<int>();
            PolicyRegistry registry = new PolicyRegistry()
            {
                { PollyConstants.RetryPolicyKey, RetryPolicy},
                { PollyConstants.CircuitBreakerPolicyKey, CircuitBreakerPolicy},
                { PollyConstants.BasicResiliencePolicyKey, Policy.WrapAsync(RetryPolicy, CircuitBreakerPolicy, TimeoutPolicy(secondaryCallTimeout)) }
            };

            return services.AddSingleton<IReadOnlyPolicyRegistry<string>>(registry);
        }

        private static IAsyncPolicy<StatusCode> RetryPolicy
        {
            get
            {
                return Policy.HandleResult<StatusCode>(r => _gRpcErrors.Contains(r))
                    .Or<BrokenCircuitException>()
                    .Or<TimeoutRejectedException>()
                    .WaitAndRetryForeverAsync((retryAttempt, context) => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (statusCodeResult, retryAttempt, timeSpan, context) =>
                    {
                        if (!context.TryGetLogger(out var logger))
                            return;

                        if (statusCodeResult.Exception != null)
                            logger.LogWarning("{exception} '{timeSpan}' seconds to wait before next '{retryAttempt}' retry.", statusCodeResult.Exception.Message, timeSpan, retryAttempt);
                        else
                            logger.LogWarning("Request failed with '{statusCode}'. '{timeSpan}' seconds to wait before next '{retryAttempt}' retry.", statusCodeResult.Result, timeSpan, retryAttempt);
                    });
            }
        }

        private static IAsyncPolicy<StatusCode> CircuitBreakerPolicy
        {
            get
            {
                return Policy.HandleResult<StatusCode>(r => _gRpcErrors.Contains(r))
                    .Or<TimeoutRejectedException>()
                    .CircuitBreakerAsync(2, TimeSpan.FromMinutes(2),
                        onBreak: (statusCodeResult, breakDuration, context) => 
                        {
                            if (!context.TryGetLogger(out var logger))
                                return;

                            if (statusCodeResult.Exception != null)
                                logger.LogWarning("{exception} Circuit breaker breaks for {breakDuration}.", statusCodeResult.Exception.Message, breakDuration);
                            else
                                logger.LogWarning("Circuit breaker breaks for {breakDuration} with '{statusCode}'.", breakDuration, statusCodeResult.Result);
                        },
                        onReset: (context) => 
                        {
                            if (!context.TryGetLogger(out var logger))
                                return;

                            logger.LogDebug("Circuit breaker reset.");
                        }
                    );
            }
        }

        private static IAsyncPolicy<StatusCode> TimeoutPolicy(int seconds) => Policy.TimeoutAsync<StatusCode>(seconds, TimeoutStrategy.Pessimistic);
    }
}
