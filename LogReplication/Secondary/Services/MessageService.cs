using Common;
using Grpc.Core;
using static Common.MessageService;

namespace Secondary.Services
{
    public class MessageService : MessageServiceBase
    {
        private readonly MessageStore _messageStore;

        public MessageService(MessageStore messageStore)
        {
            _messageStore = messageStore;
        }

        public override async Task<Response> InsertMessage(Message request, ServerCallContext context)
        {
            await _messageStore.InsertMessageAsync(request.Index, request.Value);
            return new Response();
        }
    }
}
