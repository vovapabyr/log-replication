using Common;
using Grpc.Net.ClientFactory;

namespace Master.Services
{
    public class SecondaryMessagesService
    {
        private readonly GrpcClientFactory _grpcClientFactory;
        private string[] _secondaries;
        private readonly ILogger<SecondaryMessagesService> _logger;

        public SecondaryMessagesService(GrpcClientFactory grpcClientFactory, IConfiguration configuration, ILogger<SecondaryMessagesService> logger)
        {
            _grpcClientFactory = grpcClientFactory;
            _secondaries = configuration.GetSection("Secondaries").Get<string[]>();
            _logger = logger;
        }


        public Task ForwardMessageAsync(int messageIndex, string message)
        {
            var forwardMessageTasks = new List<Task>();
            foreach (var (secName, secClient) in GetSecondariesClients())
            {
                _logger.LogInformation("Sending message to {secondary}", secName);
                forwardMessageTasks.Add(secClient.InsertMessageAsync(new Message() { Index = messageIndex, Value = message }).ResponseAsync);
            }

            return Task.WhenAll(forwardMessageTasks);
        }

        private IEnumerable<(string, MessageService.MessageServiceClient)> GetSecondariesClients() 
        {
            foreach (var sec in _secondaries)
                yield return (sec, _grpcClientFactory.CreateClient<MessageService.MessageServiceClient>(sec));
        }
    }
}
