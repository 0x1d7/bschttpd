using System.Text;
using Microsoft.AspNetCore.Http;

public class RotatingW3CLoggingMiddleware : IDisposable
{
    private readonly RequestDelegate _next;
    private readonly Timer _flushTimer;
    private readonly List<string> _logBuffer = new List<string>();
    private readonly string _logDirectory;
    private readonly TimeSpan _flushInterval;
    private const int MaxLogFileSize = 20 * 1024 * 1024; // 20 MB
    private string _currentLogFilePath;

    public RotatingW3CLoggingMiddleware(RequestDelegate next, string logDirectory, TimeSpan flushInterval)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
        _flushInterval = flushInterval;
        _flushTimer = new Timer(async _ => await FlushLogsAsync(), null, _flushInterval, _flushInterval);
        _currentLogFilePath = GetLogFilePath();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var now = DateTime.UtcNow;
        var method = context.Request.Method;
        var path = context.Request.Path;
        var queryString = context.Request.QueryString;
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        await _next(context);

        var statusCode = context.Response.StatusCode;
        var responseLength = context.Response.ContentLength ?? 0;

        var logEntry = $"{now:yyyy-MM-dd HH:mm:ss}\t{method}\t{path}\t{queryString}\t{statusCode}\t{responseLength}\t{remoteIp}";

        lock (_logBuffer)
        {
            _logBuffer.Add(logEntry);
        }
    }

    private string GetLogFilePath()
    {
        var dateSuffix = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var logFilePath = Path.Combine(_logDirectory, $"w3c-log-{dateSuffix}.txt");

        // Rotate log file if it exceeds the size limit
        if (File.Exists(logFilePath) && new FileInfo(logFilePath).Length >= MaxLogFileSize)
        {
            var timestamp = DateTime.UtcNow.ToString("HH-mm-ss");
            File.Move(logFilePath, Path.Combine(_logDirectory, $"w3c-log-{dateSuffix}-{timestamp}.txt"));
        }

        return logFilePath;
    }

    private async Task FlushLogsAsync()
    {
        List<string> logsToFlush;

        lock (_logBuffer)
        {
            logsToFlush = new List<string>(_logBuffer);
            _logBuffer.Clear();
        }

        _currentLogFilePath = GetLogFilePath();

        await using (var writer = new StreamWriter(_currentLogFilePath, true, Encoding.UTF8, 65536))
        {
            foreach (var log in logsToFlush)
            {
                await writer.WriteLineAsync(log);
            }
        }
    }

    public void Dispose()
    {
        _flushTimer.Dispose();
        FlushLogsAsync().GetAwaiter().GetResult(); // Ensure remaining logs are flushed
    }
}
