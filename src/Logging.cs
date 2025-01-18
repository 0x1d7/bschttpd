using System.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;

namespace bschttpd;

public class SqliteLogger : ILogger
{
    private readonly SqliteConnection _connection;
    private readonly string _categoryName;

    public SqliteLogger(SqliteConnection connection, string categoryName)
    {
        _connection = connection;
        _categoryName = categoryName;
    }

    public async void LogStatus(string message)
    {
        try
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }
            
            var timestamp = DateTimeOffset.UtcNow;
            var sql = $"INSERT INTO StatusLogs (Timestamp, Message) VALUES (@Timestamp, @Message)";

            using (var command = new SqliteCommand(sql, _connection))
            {
                command.Parameters.AddWithValue("@Timestamp", timestamp);
                command.Parameters.AddWithValue("@Message", message);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async void LogException(LogLevel logLevel, EventId eventId, Exception exception)
    {
        try
        {
            if (exception == null)
            {
                return;
            }
            
            var timestamp = DateTimeOffset.UtcNow;
            var sql = $"INSERT INTO ExceptionLogs (LogLevel, Timestamp, Message) VALUES (@LogLevel, @Timestamp, @Message)";

            using (var command = new SqliteCommand(sql, _connection))
            {
                command.Parameters.AddWithValue("@LogLevel", logLevel.ToString());
                command.Parameters.AddWithValue("@Timestamp", timestamp);
                command.Parameters.AddWithValue("@Message", exception.Message);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task LogW3C(W3CLogEntry logEntry)
    {
        try
        {
            var command = _connection.CreateCommand();
            command.CommandText =
                @"INSERT INTO W3CLogs (Date, Time, 's-sitename', 's-computername', 's-ip', 'cs-method', 'cs-uri-stem', " +
                "'cs-uri-query', 's-port', 'cs-username', 'c-ip', 'cs-version', 'cs(User-Agent)', 'cs(Cookie)', " +
                "'cs(Referrer)', 'cs-host', 'sc-status', 'sc-substatus', 'sc-win32-status', 'sc-bytes', 'cs-bytes', 'time-taken', streamid) " +
                "VALUES ($date, $time, $ssitename, $scomputername, $sip, $csmethod, $csuristem, $csuriquery, $sport, $csusername, $cip, " +
                "$csversion, $csuseragent, $cscookie, $csreferrer, $cshost, $scstatus, $scsubstatus, $scwin32status, $scbytes, $csbytes, " +
                " $timetaken, $streamid)";
            
     
                command.Parameters.AddWithValue("$date", logEntry.Date);
                command.Parameters.AddWithValue("$time", logEntry.Time);
                command.Parameters.AddWithValue("$ssitename", logEntry.ssitename);
                command.Parameters.AddWithValue("$scomputername", logEntry.scomputername);
                command.Parameters.AddWithValue("$sip", logEntry.sip);
                command.Parameters.AddWithValue("$csmethod", logEntry.csmethod);
                command.Parameters.AddWithValue("$csuristem", logEntry.csuristem);
                command.Parameters.AddWithValue("$csuriquery", logEntry.csuriquery);
                command.Parameters.AddWithValue("$sport", logEntry.sport);
                command.Parameters.AddWithValue("$csusername", logEntry.csusername);
                command.Parameters.AddWithValue("$cip", logEntry.cip);
                command.Parameters.AddWithValue("$csversion", logEntry.csversion);
                command.Parameters.AddWithValue("$csuseragent", logEntry.csuseragent);
                command.Parameters.AddWithValue("$cscookie", logEntry.cscookie);
                command.Parameters.AddWithValue("$csreferrer", logEntry.csreferrer);
                command.Parameters.AddWithValue("$cshost", logEntry.cshost);
                command.Parameters.AddWithValue("$scstatus", logEntry.scstatus);
                command.Parameters.AddWithValue("$scsubstatus", logEntry.scsubstatus);
                command.Parameters.AddWithValue("$scwin32status", logEntry.scwin32status);
                command.Parameters.AddWithValue("$scbytes", logEntry.csbytes);
                command.Parameters.AddWithValue("$csbytes", logEntry.csbytes);
                command.Parameters.AddWithValue("$timetaken", logEntry.timetaken);
                command.Parameters.AddWithValue("$streamid", logEntry.streamid);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    
    #pragma warning disable CS8767
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
        Func<TState, Exception, string> formatter)
    {
        LogException(logLevel, eventId, exception);
    } 
    #pragma warning restore CS8767
    
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

#pragma warning disable CS8633 // Nullability in constraints for type parameter doesn't match the constraints for type parameter in implicitly implemented interface method'.
    public IDisposable BeginScope<TState>(TState state)
#pragma warning restore CS8633 // Nullability in constraints for type parameter doesn't match the constraints for type parameter in implicitly implemented interface method'.
    {
        return null!;
    }
}
