using Common;
using Grpc.Core;
using Grpc.Net.ClientFactory;
using Master.Extensions;
using Polly;
using Polly.Registry;

namespace Master.Services
{
    public class MessageBroadcastService
    {
        private readonly MessageStore _messageStore;
        private readonly GrpcClientFactory _grpcClientFactory;
        private string[] _secondaries;
        private readonly int _masterWriteDelay;
        private readonly IReadOnlyPolicyRegistry<string> _policyRegistry;
        private readonly ILogger<MessageBroadcastService> _logger;

        public MessageBroadcastService(MessageStore messageStore, GrpcClientFactory grpcClientFactory, IConfiguration configuration, IReadOnlyPolicyRegistry<string> policyRegistry, ILogger<MessageBroadcastService> logger)
        {
            _messageStore = messageStore;
            _grpcClientFactory = grpcClientFactory;
            _secondaries = configuration.GetSection("Secondaries").Get<string[]>();
            _masterWriteDelay = configuration.GetSection("WriteDelay").Get<int>();
            _policyRegistry = policyRegistry;
            _logger = logger;
        }


        public async Task BroadcastMessageAsync(string message, int writeConcern, int broadcastDelay)
        {
            var nextMessageIndex = _messageStore.GetNextMessageIndex();
            _logger.LogInformation("TOTAL ORDER OF MESSAGE '{message}' IS '{index}'.", message, nextMessageIndex);
            if (broadcastDelay > 0)
            {
                _logger.LogInformation("Delaying message broadcast to '{delay}' ms.", broadcastDelay);
                Thread.Sleep(broadcastDelay);
            }

            var isValidWriteConcern = writeConcern <= _secondaries.Length + 1; // secondaries + master
            if (!isValidWriteConcern) 
            {
                _logger.LogWarning("Write concern '{writeConcern}' is bigger than number of secondaries '{secondariesCount}'. Setting write concern to 0.", writeConcern, _secondaries.Length);
                writeConcern = 0;
            }

            // Number of secondaries to wait with the help of cde. Master is awaited separately.
            var secondariesToWaitCount = writeConcern > 0 ? writeConcern - 1 : writeConcern;
            // Always wait master when writeConcern > 0;
            var waitMaster = writeConcern > 0;

            var cde = new CountdownEvent(secondariesToWaitCount);
            try 
            {
                var masterTask = Task.Run(async () => 
                {
                    if (_masterWriteDelay > 0)
                    {
                        _logger.LogInformation("Delaying message write to '{delay}' ms.", _masterWriteDelay);
                        Thread.Sleep(_masterWriteDelay);
                    }
                    await _messageStore.InsertMessageAsync(nextMessageIndex, message);
                });
                foreach (var (secName, secClient) in GetSecondariesClients())
                {
                    var resiliencePolicy = _policyRegistry.Get<IAsyncPolicy<StatusCode>>(PollyConstants.GetSecondaryResiliencePolicyKey(secName));
                    _logger.LogInformation("Sending message '{message}' to '{secondary}' secondary.", message, secName);
                    _ = resiliencePolicy.ExecuteAsync((ctx) => secClient.InsertMessageAsync(new Message() { Index = nextMessageIndex, Value = message }).WaitForStatusAsync(_logger), new Context($"Adding message {message} to {secName}", 
                        new Dictionary<string, object>()
                        {
                            { PollyConstants.LoggerKey, _logger }
                        })).ContinueWithCDESignal(message, cde, secName, _logger);
                }

                if(waitMaster)
                    await masterTask;

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
