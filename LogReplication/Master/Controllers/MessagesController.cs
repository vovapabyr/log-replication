using Common;
using Master.Services;
using Microsoft.AspNetCore.Mvc;

namespace Master.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MessagesController : ControllerBase
    {
        private readonly MessageStore _messageStore;
        private readonly MessageBroadcastService _messageBroadcastService;
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(MessageStore messageStore, MessageBroadcastService messageBroadcastService, ILogger<MessagesController> logger)
        {
            _messageStore = messageStore;
            _messageBroadcastService = messageBroadcastService;
            _logger = logger;
        }

        [HttpGet]
        public IAsyncEnumerable<string> Get()
        {
            return _messageStore.GetMessagesAsync();
        }

        [HttpPost]
        public async Task Post([FromForm] string message, [FromForm] int writeConcern)
        {            
            _logger.LogInformation("START MESSAGE '{message}' BROADCAST. Write concern '{writeConcern}'.", message, writeConcern);            
            await _messageBroadcastService.BroadcastMessageAsync(message, writeConcern);
            _logger.LogInformation("END MESSAGE '{message}' BROADCAST.", message);
        }
    }
}