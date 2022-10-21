using Common;
using Microsoft.AspNetCore.Mvc;

namespace Master.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MessagesController : ControllerBase
    {
        private readonly MessageService _messageService;
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(MessageService messageService, ILogger<MessagesController> logger)
        {
            _messageService = messageService;
            _logger = logger;
        }

        [HttpGet]
        public IAsyncEnumerable<string> Get()
        {
            return _messageService.GetMessages();
        }

        [HttpPost]
        public Task Post([FromForm] string message, [FromForm] int index)
        {
            return _messageService.InsertMessageAsync(index, message);
        }
    }
}