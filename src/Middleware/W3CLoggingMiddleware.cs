using System.Text;
using bschttpd.Properties;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace bschttpd;

public class RotatingW3CLoggingMiddleware : IDisposable
{
    private readonly RequestDelegate _next;
    private readonly Timer _flushTimer;
    private readonly List<string> _logBuffer = [];
    private readonly string _logDirectory;
    private readonly string _logFileName;
    private const int MaxLogFileSize = 20 * 1024 * 1024; // 20 MB
    private string _currentLogFilePath;

    public RotatingW3CLoggingMiddleware(RequestDelegate next, IOptions<WebServerConfiguration> webServerConfiguration, 
        IHostApplicationLifetime applicationLifetime)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logDirectory = webServerConfiguration.Value.W3CLogDirectory;
        _logFileName = webServerConfiguration.Value.W3CLogName;
        var flushInterval = TimeSpan.FromSeconds(webServerConfiguration.Value.W3CLogFlushInterval);
        _flushTimer = new Timer(async _ => await FlushLogsAsync(), null, flushInterval, flushInterval);
        _currentLogFilePath = GetLogFilePath();
        
        applicationLifetime.ApplicationStopping.Register(OnShutdown);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var now = DateTime.UtcNow;
        var date = now.ToString("yyyy-MM-dd");
        var time = now.ToString("HH:mm:ss");
        var method = context.Request.Method ?? "-";
        var path = string.IsNullOrWhiteSpace(context.Request.Path) ? "-" : context.Request.Path.ToString();
        var queryString = string.IsNullOrWhiteSpace(context.Request.QueryString.ToString()) ? "-" : context.Request.QueryString.ToString();
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "-";
        var userAgentHeaders = context.Request.Headers.UserAgent;

        var userAgent = userAgentHeaders.ToString();

        if (StringValues.IsNullOrEmpty(userAgentHeaders))
            userAgent = "-";
        
        await _next(context);

        var statusCode = context.Response.StatusCode;

        //don't log content-length: https://github.com/dotnet/aspnetcore/issues/47127#issuecomment-1468910600
        
        var logEntry = $"{date} {time} {method} {path} {queryString} {statusCode} {remoteIp} {userAgent}";

        lock (_logBuffer)
        {
            _logBuffer.Add(logEntry);
        }
    }

    private string GetLogFilePath()
    {
        var dateSuffix = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var logFilePath = Path.Combine(_logDirectory, $"{_logFileName}-{dateSuffix}.log");
        var isNewFile = !File.Exists(logFilePath);

        if (File.Exists(logFilePath) && new FileInfo(logFilePath).Length >= MaxLogFileSize)
        {
            var timestamp = DateTime.UtcNow.ToString("HH-mm-ss");
            File.Move(logFilePath, Path.Combine(_logDirectory, $"{_logFileName}-{dateSuffix}-{timestamp}.log"));
            isNewFile = true;
        }

        if (isNewFile)
        {
            _ = WriteLogFileHeader(logFilePath);
        }

        return logFilePath;
    }
    
    private static async Task WriteLogFileHeader(string filePath)
    {
        var header = new StringBuilder();
        header.AppendLine("#Software: Basic Httpd/1.0");
        header.AppendLine("#Version: 1.0");
        header.AppendLine($"#Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        header.AppendLine("#Fields: date time cs-method uri-stem uri-query status c-ip cs(UserAgent)");

        await File.WriteAllTextAsync(filePath, header.ToString());
    }
    
    private async Task FlushLogsAsync()
    {
        try
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
        catch (Exception)
        {
            //ToDo: Add ILogger
        }
    }

    public void OnShutdown()
    {
        Dispose();
    }
    
    public void Dispose()
    {
        _flushTimer.Dispose();
        FlushLogsAsync().GetAwaiter().GetResult(); // Ensure remaining logs are flushed
    }
}