using Common;
using Grpc.Core;
using HealthChecks.UI.Core;
using Newtonsoft.Json;
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

        public static IServiceCollection ConfigureResiliencePolicies(this IServiceCollection services, IConfiguration configuration)
        {
            var secondaryCallTimeout = configuration.GetSection("SecondaryCallTimeout").Get<int>();
            var secondaries = configuration.GetSection("Secondaries").Get<string[]>();
            PolicyRegistry registry = new PolicyRegistry();
            // Need to add resilience policy for each secondary separately, as circuit breaker breaks individually for each secondary.
            foreach (var sec in secondaries)
                registry.Add(PollyConstants.GetSecondaryResiliencePolicyKey(sec), Policy.WrapAsync(RetryPolicy, CircuitBreakerPolicy, TimeoutPolicy(secondaryCallTimeout)));

            return services.AddSingleton<IReadOnlyPolicyRegistry<string>>(registry);
        }

        public static IServiceCollection ConfigureHealthChecksUI(this IServiceCollection services, IConfiguration configuration)
        {
            var secondaries = configuration.GetSection("Secondaries").Get<string[]>();
            services.AddHealthChecksUI(settings =>
            {
                // Register health checks to secondaries.
                foreach (var sec in secondaries)
                {
                    settings.AddHealthCheckEndpoint(sec, $"https://{sec}:443/hc");
                }

                settings.UseApiEndpointHttpMessageHandler(s => new HttpClientHandler() { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator });
                // Set secondary health check timeout to 2 sec.
                settings.ConfigureApiEndpointHttpclient((s, h) => h.Timeout = TimeSpan.FromSeconds(2));
                // Check secondary health every 3 sec.
                settings.SetEvaluationTimeInSeconds(3);

                // Configure web hook to notify health status change to control secondaries resilience policies.
                settings.AddWebhookNotification("Master Resilience Policy Control",
                    uri: "https://master:443/policy",
                    payload: JsonConvert.SerializeObject(new { Name = "[[LIVENESS]]", Status = "[[FAILURE]]", Descriptions = "[[DESCRIPTIONS]]" }),
                    restorePayload: JsonConvert.SerializeObject(new { Name = "[[LIVENESS]]", Status = UIHealthStatus.Healthy.ToString(), Descriptions = "[[LIVENESS]] is healthy again." }),
                    customMessageFunc: (livenessName, report) => report.Status.ToString(),
                    customDescriptionFunc: (livenessName, report) => report.Entries.First().Value.Description);

                settings.UseWebhookEndpointHttpMessageHandler(s => new HttpClientHandler() { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator });
            }).AddInMemoryStorage();

            return services;
        }

        public static IServiceCollection ConfigureGrpcClients(this IServiceCollection services, IConfiguration configuration)
        {
            var secondaries = configuration.GetSection("Secondaries").Get<string[]>();
            foreach (var sec in secondaries)
            {
                services.AddGrpcClient<MessageService.MessageServiceClient>(sec, o =>
                {
                    o.Address = new Uri($"https://{sec}:443");
                }).ConfigureChannel(c =>
                {
                    c.HttpHandler = new HttpClientHandler() { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
                });
            }

            return services;
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
                    .CircuitBreakerAsync(int.MaxValue, TimeSpan.MaxValue,
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
