using Common;
using Grpc.Net.ClientFactory;
using Master.Extensions;

namespace Master.Services
{
    public class MessageBroadcastService
    {
        private readonly MessageStore _messageStore;
        private readonly GrpcClientFactory _grpcClientFactory;
        private string[] _secondaries;
        private readonly int _masterWriteDelay;
        private readonly ILogger<MessageBroadcastService> _logger;

        public MessageBroadcastService(MessageStore messageStore, GrpcClientFactory grpcClientFactory, IConfiguration configuration, ILogger<MessageBroadcastService> logger)
        {
            _messageStore = messageStore;
            _grpcClientFactory = grpcClientFactory;
            _secondaries = configuration.GetSection("Secondaries").Get<string[]>();
            _masterWriteDelay = configuration.GetSection("WriteDelay").Get<int>();
            _logger = logger;
        }


        public async Task BroadcastMessageAsync(string message, int writeConcern)
        {                     
            var isValidWriteConcern = writeConcern <= _secondaries.Length + 1; // secondaries + master
            if (!isValidWriteConcern) 
            {
                _logger.LogWarning("Write concern '{writeConcern}' is bigger than number of secondaries '{secondariesCount}'. Setting write concern to 0.", writeConcern, _secondaries.Length);
                writeConcern = 0;
            }

            var cde = new CountdownEvent(writeConcern);
            try 
            {
                var nextMessageIndex = await _messageStore.GetNextIndexAsync();
                _ = Task.Run(() => 
                {
                    if (_masterWriteDelay > 0)
                    {
                        _logger.LogInformation("Delaying message write to '{delay}' ms.", _masterWriteDelay);
                        Thread.Sleep(_masterWriteDelay);
                    }
                    _messageStore.InsertMessageAsync(nextMessageIndex, message).ContinueWithCDESignal(message, cde, "master", _logger);
                });
                foreach (var (secName, secClient) in GetSecondariesClients())
                {
                    _logger.LogInformation("Sending message '{message}' to '{secondary}' secondary.", message, secName);
                    _ = secClient.InsertMessageAsync(new Message() { Index = nextMessageIndex, Value = message }).ResponseAsync.ContinueWithCDESignal(message, cde, secName, _logger);
                }

                await Task.Run(() => cde.Wait());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            finally
            {
                cde.Dispose();
                _logger.LogDebug("CDE is disposed.");
            }      
        }

        private IEnumerable<(string, MessageService.MessageServiceClient)> GetSecondariesClients() 
        {
            foreach (var sec in _secondaries)
                yield return (sec, _grpcClientFactory.CreateClient<MessageService.MessageServiceClient>(sec));
        }
    }
}
