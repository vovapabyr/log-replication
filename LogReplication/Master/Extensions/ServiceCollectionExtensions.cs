using Grpc.Core;
using Polly;
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
                { PollyConstants.RetryPolicyKey,  RetryPolicy}
            };

            return services.AddSingleton<IReadOnlyPolicyRegistry<string>>(registry);
        }

        private static IAsyncPolicy<StatusCode> RetryPolicy
        {
            get
            {
                return Policy.HandleResult<StatusCode>(r => _gRpcErrors.Contains(r))
                    .WaitAndRetryForeverAsync((retryAttempt, context) => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (statusCodeResut, retryAttempt, timeSpan, context) =>
                    {
                        if (!context.TryGetLogger(out var logger))
                            return;

                        logger.LogDebug("Request failed with '{statusCode}'. '{timeSpan}' seconds to wait before next '{retryAttempt}' retry.", statusCodeResut.Result, timeSpan, retryAttempt);
                    });
            }
        }
    }
}
