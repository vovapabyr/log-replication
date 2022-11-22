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


        public async Task ForwardMessageAsync(int messageIndex, string message, int writeConcern)
        {
            var isValidWriteConcern = writeConcern <= _secondaries.Length;
            if (!isValidWriteConcern) 
            {
                _logger.LogWarning("Write concern '{writeConcern}' is bigger than number of secondaries '{secondariesCount}'. Setting write concern to 0.", writeConcern, _secondaries.Length);
                writeConcern = 0;
            }

            var cde = new CountdownEvent(writeConcern);
            try 
            {
                foreach (var (secName, secClient) in GetSecondariesClients())
                {
                    _logger.LogInformation("Sending message '{message}' to '{secondary}' secondary.", message, secName);
                    _ = secClient.InsertMessageAsync(new Message() { Index = messageIndex, Value = message }).ResponseAsync.ContinueWith(r =>
                    {
                        _logger.LogInformation("Message '{message}' is delivered to '{secondary}' secondary.", message, secName);
                        if (!cde.IsSet)
                        {
                            cde.Signal();
                            _logger.LogDebug("Secondary '{secondary}' signaled to CDE. CDE's current count: '{count}.", secName, cde.CurrentCount);
                        }
                    });
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
