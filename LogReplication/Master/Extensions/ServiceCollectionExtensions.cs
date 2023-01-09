using Grpc.Core;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;

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

        public static IServiceCollection ConfigurePollyPolicies(this IServiceCollection services)
        {
            PolicyRegistry registry = new PolicyRegistry()
            {
                { PollyConstants.RetryPolicyKey,  RetryPolicy},
                { PollyConstants.CircuitBreakerPolicyKey,  CircuitBreakerPolicy},
                { PollyConstants.BasicResiliencePolicyKey,  Policy.WrapAsync(RetryPolicy, CircuitBreakerPolicy) }
            };

            return services.AddSingleton<IReadOnlyPolicyRegistry<string>>(registry);
        }

        private static IAsyncPolicy<StatusCode> RetryPolicy
        {
            get
            {
                return Policy.HandleResult<StatusCode>(r => _gRpcErrors.Contains(r))
                    .Or<BrokenCircuitException>()
                    .WaitAndRetryForeverAsync((retryAttempt, context) => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (statusCodeResut, retryAttempt, timeSpan, context) =>
                    {
                        if (!context.TryGetLogger(out var logger))
                            return;

                        if (statusCodeResut.Exception != null)
                            logger.LogWarning("{exception} '{timeSpan}' seconds to wait before next '{retryAttempt}' retry.", statusCodeResut.Exception.Message, timeSpan, retryAttempt);
                        else
                            logger.LogWarning("Request failed with '{statusCode}'. '{timeSpan}' seconds to wait before next '{retryAttempt}' retry.", statusCodeResut.Result, timeSpan, retryAttempt);
                    });
            }
        }

        private static IAsyncPolicy<StatusCode> CircuitBreakerPolicy
        {
            get
            {
                return Policy.HandleResult<StatusCode>(r => _gRpcErrors.Contains(r))
                    .CircuitBreakerAsync(2, TimeSpan.FromMinutes(2),
                        onBreak: (statusCodeResult, breakDuration, context) => 
                        {
                            if (!context.TryGetLogger(out var logger))
                                return;

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
    }
}
