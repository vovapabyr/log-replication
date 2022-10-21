using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Common
{
    public class MessageService 
    {
        private readonly List<string> _messages = new List<string>(100);

        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1); 

        private ILogger<MessageService> _logger;

        public MessageService(ILogger<MessageService> logger)
        {
            _logger = logger;
        }

        public async Task<int> AddMessageAsync(string message)
        {
            try 
            {
                _logger.LogDebug("Waiting message {message} to append.", message);
                await _semaphoreSlim.WaitAsync();
                _messages.Add(message);
                _logger.LogInformation("Message {message} appended.", message);
                return GetLastIndex();
            }
            finally 
            {
                _logger.LogDebug("Releasing lock after {message} append.", message);
                _semaphoreSlim.Release();
            }
        }

        public async Task InsertMessageAsync(int messageIndex, string message)
        {
            try
            {
                _logger.LogDebug("Waiting message {message} to insert at {index}.", message, messageIndex);
                await _semaphoreSlim.WaitAsync();
                EnsureSize(messageIndex);
                _messages[messageIndex] = message;
                _logger.LogInformation("Message {message} inserted at {index}.", message, messageIndex);
            }
            finally 
            {
                _logger.LogDebug("Releasing lock after {message} insert at {index}.", message, messageIndex);
                _semaphoreSlim.Release();
            }            
        }

        public async IAsyncEnumerable<string> GetMessages() 
        {
            try 
            {
                _logger.LogDebug("Waiting to read messages.");
                await _semaphoreSlim.WaitAsync();
                for (int i = 0; i < _messages.Count; i++)
                {
                    var message = _messages[i];
                    if (string.IsNullOrEmpty(message))
                    {
                        _logger.LogWarning("Size {size}. Missing element at {index}.", _messages.Count, i);
                        yield break;
                    }

                    _logger.LogDebug("Reading {message} at {index}.", message, i);
                    yield return message;
                }
            }
            finally 
            {
                _logger.LogDebug("Releasing lock after reading messages.");
                _semaphoreSlim.Release();
            }
        }

        #region private

        private void EnsureSize(int messageIndex)
        {
            while (messageIndex > GetLastIndex())
            {
                _messages.Add(default);
                _logger.LogWarning("Resizing to {size}.", _messages.Count);
            }
        }

        private int GetLastIndex() => _messages.Count - 1;

        #endregion
    }
}
