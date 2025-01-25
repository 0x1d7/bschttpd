using Microsoft.Extensions.Logging;

namespace bschttpd
{
    public class FileLoggingProvider : ILogger
    {
        private readonly string _filePath;
        private readonly string _categoryName;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public FileLoggingProvider(string filePath, string categoryName)
        {
            _filePath = filePath;
            _categoryName = categoryName;
        }

        private async Task LogToFileAsync(string message)
        {
            await _semaphore.WaitAsync();
            try
            {
                await using (var writer = new StreamWriter(_filePath, true))
                {
                    await writer.WriteLineAsync($"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss.fff zzz} [{_categoryName}] {message}");
                    await writer.FlushAsync();
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, 
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);

            _ = LogToFileAsync($"{logLevel}: {message}");

            if (exception != null)
            {
                _ = LogToFileAsync($"Exception: {exception}");
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Information;
        }

        public IDisposable BeginScope<TState>(TState state) => null!;
    }

    public class FileLoggingProviderProvider : ILoggerProvider
    {
        private readonly string _errorFilePath;
        private readonly string _statusFilePath;

        public FileLoggingProviderProvider(string errorFilePath, string statusFilePath)
        {
            _errorFilePath = errorFilePath;
            _statusFilePath = statusFilePath;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLoggingProvider(categoryName.Contains("Error") ? _errorFilePath : _statusFilePath, categoryName);
        }

        public void Dispose()
        {
        }
    }
}
