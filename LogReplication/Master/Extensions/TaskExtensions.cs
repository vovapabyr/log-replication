using Master.Services;

namespace Master.Extensions
{
    public static class TaskExtensions
    {
        public static Task ContinueWithCDESignal(this Task task, string message, CountdownEvent cde, string nodeName, ILogger<MessageBroadcastService> logger)
        {
            return task.ContinueWith(t =>
            {
                logger.LogInformation("Message '{message}' is delivered to '{nodeName}' node.", message, nodeName);
                if (!cde.IsSet)
                {
                    cde.Signal();
                    logger.LogInformation("Node '{nodeName}' signaled to CDE. Waiting for '{count}' more nodes to finish.", nodeName, cde.CurrentCount);
                }
            });
        }
    }
}
