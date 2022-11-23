using Common;
using Master.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;

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
        public async Task<ActionResult> Post([FromForm] string message, [FromForm] int writeConcern, [FromForm] int broadcastDelay)
        {
            if (writeConcern < 0)
                return BadRequest("Invalid write concern. Write concern should be greater than 0.");

            _logger.LogInformation("START MESSAGE '{message}' BROADCAST. Write concern '{writeConcern}'.", message, writeConcern);            
            await _messageBroadcastService.BroadcastMessageAsync(message, writeConcern, broadcastDelay);
            _logger.LogInformation("END MESSAGE '{message}' BROADCAST.", message);

            return StatusCode((int)HttpStatusCode.Created);
        }
    }
}