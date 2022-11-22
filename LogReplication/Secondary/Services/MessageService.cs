using Common;
using Grpc.Core;
using static Common.MessageService;

namespace Secondary.Services
{
    public class MessageService : MessageServiceBase
    {
        private readonly MessageStore _messageStore;
        private readonly ILogger<MessageService> _logger;
        private readonly int _writeDelay;

        public MessageService(MessageStore messageStore, IConfiguration configuration, ILogger<MessageService> logger)
        {
            _messageStore = messageStore;
            _logger = logger;
            _writeDelay = configuration.GetSection("WriteDelay").Get<int>();
        }

        public override async Task<Response> InsertMessage(Message request, ServerCallContext context)
        {
            if (_writeDelay > 0) 
            {
                _logger.LogInformation("Delaying message write to '{delay}' ms.", _writeDelay);
                Thread.Sleep(_writeDelay);
            }
            await _messageStore.InsertMessageAsync(request.Index, request.Value);
            return new Response();
        }
    }
}
