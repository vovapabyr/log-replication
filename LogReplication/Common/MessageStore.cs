using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Common
{
    public class MessageStore 
    {
        private readonly List<string> _messages = new List<string>(100);

        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1); 

        private ILogger<MessageStore> _logger;

        public MessageStore(ILogger<MessageStore> logger)
        {
            _logger = logger;
        }

        public async Task<int> AddMessageAsync(string message)
        {
            try 
            {
                _logger.LogDebug("Waiting message '{message}' to append.", message);
                await _semaphoreSlim.WaitAsync();
                _messages.Add(message);
                _logger.LogInformation("Message '{message}' is appended.", message);
                return GetLastIndex();
            }
            finally 
            {
                _logger.LogDebug("Releasing lock after '{message}' appended.", message);
                _semaphoreSlim.Release();
            }
        }

        public async Task InsertMessageAsync(int messageIndex, string message)
        {
            try
            {
                _logger.LogDebug("Waiting message '{message}' to insert at '{index}' index.", message, messageIndex);
                await _semaphoreSlim.WaitAsync();
                if (_messages.Count == messageIndex)
                    _messages.Add(message);
                else
                {
                    EnsureSize(messageIndex);
                    _messages[messageIndex] = message;
                }
                _logger.LogInformation("Message '{message}' inserted at '{index}' index.", message, messageIndex);
            }
            finally 
            {
                _logger.LogDebug("Releasing lock after message '{message}' inserted at '{index}' index.", message, messageIndex);
                _semaphoreSlim.Release();
            }            
        }

        public async IAsyncEnumerable<string> GetMessagesAsync() 
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
                        _logger.LogWarning("Size '{size}'. Missing element at '{index}' index.", _messages.Count, i);
                        yield break;
                    }

                    _logger.LogDebug("Reading message '{message}' at '{index}' index.", message, i);
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
                _logger.LogWarning("Resizing to '{size}'.", _messages.Count);
            }
        }

        private int GetLastIndex() => _messages.Count - 1;

        #endregion
    }
}
