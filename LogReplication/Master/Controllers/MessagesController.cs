using Common;
using Master.Services;
using Microsoft.AspNetCore.Mvc;

namespace Master.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MessagesController : ControllerBase
    {
        private readonly MessageStore _messageService;
        private readonly SecondaryMessagesService _secondaryMessagesService;
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(MessageStore messageService, SecondaryMessagesService secondaryMessagesService, ILogger<MessagesController> logger)
        {
            _messageService = messageService;
            _secondaryMessagesService = secondaryMessagesService;
            _logger = logger;
        }

        [HttpGet]
        public IAsyncEnumerable<string> Get()
        {
            return _messageService.GetMessagesAsync();
        }

        [HttpPost]
        public async Task Post([FromForm] string message, [FromForm] int writeConcern)
        {            
            _logger.LogInformation("Write concern {writeConcern} on adding messsage {message}.", writeConcern, message);
            var messageIndex = await _messageService.AddMessageAsync(message);
            await _secondaryMessagesService.ForwardMessageAsync(messageIndex, message, writeConcern);
        }
    }
}