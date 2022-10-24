using Common;
using Grpc.Net.Client;
using Grpc.Net.ClientFactory;

namespace Master.Services
{
    public class SecondaryClientsFactory
    {
        private readonly GrpcClientFactory _grpcClientFactory;
        private string[] _secondaries;

        public SecondaryClientsFactory(GrpcClientFactory grpcClientFactory, IConfiguration configuration)
        {
            _grpcClientFactory = grpcClientFactory;
            _secondaries = configuration.GetSection("Secondaries").Get<string[]>();
        }

        public IEnumerable<MessageService.MessageServiceClient> GetSecondariesClients() 
        {
            foreach (var sec in _secondaries)
                yield return _grpcClientFactory.CreateClient<MessageService.MessageServiceClient>(sec);
        }
    }
}
