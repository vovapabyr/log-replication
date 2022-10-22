using Common;
using Microsoft.AspNetCore.Mvc;

namespace Master.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MessagesController : ControllerBase
    {
        private readonly MessageStore _messageService;
        private readonly IEnumerable<MessageService.MessageServiceClient> _secondaryClients;
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(MessageStore messageService, IEnumerable<MessageService.MessageServiceClient> secondaryClients, ILogger<MessagesController> logger)
        {
            _messageService = messageService;
            _secondaryClients = secondaryClients;
            _logger = logger;
        }

        [HttpGet]
        public IAsyncEnumerable<string> Get()
        {
            return _messageService.GetMessages();
        }

        [HttpPost]
        public async Task Post([FromForm] string message, [FromForm]int delay)
        {            
            var messageIndex = await _messageService.AddMessageAsync(message);
            Thread.Sleep(delay);
            await ForwardMessageToSecondaries(messageIndex, message);
        }

        private Task ForwardMessageToSecondaries(int messageIndex, string message) 
        {
            var forwardMessageTasks = new List<Task>();
            foreach (var secondaryClient in _secondaryClients)
                forwardMessageTasks.Add(secondaryClient.InsertMessageAsync(new Message() { Index = messageIndex, Value = message }).ResponseAsync);

            return Task.WhenAll(forwardMessageTasks);
        }
    }
}