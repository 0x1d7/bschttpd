using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

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
