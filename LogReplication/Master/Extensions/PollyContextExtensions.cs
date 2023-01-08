using Polly;

namespace Master.Extensions
{
    internal static class PollyContextExtensions
    {
        public static bool TryGetLogger(this Context context, out ILogger logger)
        {
            logger = null;
            if (context.TryGetValue(PollyConstants.LoggerKey, out var loggerObject) && loggerObject is ILogger theLogger)
            {
                logger = theLogger;
                return true;
            }

            return false;
        }
    }
}
