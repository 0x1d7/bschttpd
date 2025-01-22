using Microsoft.Extensions.Logging;

namespace bschttpd
{
    public class FileLoggingProvider(string filePath, string categoryName) : ILogger
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private async Task LogToFileAsync(string message)
        {
            // ReSharper disable once ConvertToUsingDeclaration
            await using (var writer = new StreamWriter(filePath, true))
            {
                await writer.WriteLineAsync($"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss.fff zzz} [{categoryName}] {message}");
                await writer.FlushAsync(); // Ensure all data is written to the file
            }
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);

            Task.Run(async () =>
            {
                await _semaphore.WaitAsync(); // Async-friendly lock
                try
                {
                    await LogToFileAsync($"{logLevel}: {message}");

                    if (exception != null)
                    {
                        await LogToFileAsync($"Exception: {exception}");
                    }
                }
                catch (Exception ex)
                {
                    // Handle or log the exception as needed
                    Console.WriteLine($"Logging failed: {ex}");
                }
                finally
                {
                    _semaphore.Release();
                }
            });
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Information;
        }

        IDisposable ILogger.BeginScope<TState>(TState state)
        {
            return null!;
        }
    }

    public class FileLoggingProviderProvider(string errorFilePath, string statusFilePath) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new FileLoggingProvider(categoryName.Contains("Error") ? errorFilePath : statusFilePath, categoryName);
        }

        public void Dispose()
        {
        }
    }
}
