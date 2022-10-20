using Microsoft.AspNetCore.Mvc;

namespace Master.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MessagesController : ControllerBase
    {
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(ILogger<MessagesController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IEnumerable<string> Get()
        {
            _logger.LogDebug("Getting all the messages");

            return new string[] { "Static", "Messages" };
        }
    }
}