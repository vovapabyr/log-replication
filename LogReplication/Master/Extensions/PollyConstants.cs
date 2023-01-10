namespace Master.Extensions
{
    public class PollyConstants
    {
        public const string RetryPolicyKey = "RetryPolicy";
        public const string CircuitBreakerPolicyKey = "CircuitBreakerPolicy";
        public const string BasicResiliencePolicyKey = "BasicResiliencePolicy";
        public const string LoggerKey = "Logger";

        public static string GetSecondaryResiliencePolicyKey(string secName) => $"{BasicResiliencePolicyKey}-{secName}";
    }
}
