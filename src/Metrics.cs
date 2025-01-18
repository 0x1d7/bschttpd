using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
// ReSharper disable InconsistentNaming
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace bschttpd;

public class Metrics
{
    private readonly RequestDelegate _next;
    private readonly SqliteLogger _sqliteLogger;

    public Metrics(RequestDelegate next, ILogger<Metrics> logger, SqliteLogger sqliteLogger)
    {
        _next = next;
        _sqliteLogger = sqliteLogger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;
        
        await _next(context);

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;
        
        var logEntry = new W3CLogEntry()
        {
            Date = startTime.ToString("yyyy-MM-dd"),
            Time = startTime.ToString("HH:mm:ss.fff"),
            ssitename = "",
            scomputername = System.Environment.MachineName,
            sip = "",
            csmethod = context.Request.Method,
            csuristem = context.Request.Path.ToString(),
            csuriquery = context.Request.QueryString.ToString(),
            sport = 0,
            csusername = context.User.Identity?.Name ?? "",
            cip = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
            csversion = context.Request.Protocol,
            csuseragent = context.Request.Headers["User-Agent"].ToString(),
            cscookie = "",
            csreferrer = context.Request.Headers["Referer"].ToString(),
            cshost = "",
            scstatus = context.Response.StatusCode,
            scsubstatus = 0,
            scwin32status = "",
            scbytes = "0",
            csbytes = context.Request.ContentLength.ToString() ?? "0",
            timetaken = duration.TotalSeconds.ToString(CultureInfo.InvariantCulture),
            streamid = "0"
        };

        await _sqliteLogger.LogW3C(logEntry);
    }
}

public class W3CLogEntry
{
    public string Date { get; set; }
    public string Time { get; set; }
    public string ssitename { get; set; }
    public string scomputername { get; set; }
    public string sip { get; set; }
    public string csmethod { get; set; }
    public string csuristem { get; set; }
    public string csuriquery { get; set; }
    public int sport { get; set; }
    public string csusername { get; set; }
    public string cip { get; set; }
    public string csversion { get; set; }
    public string csuseragent { get; set; }
    public string cscookie { get; set; }
    public string csreferrer { get; set; }
    public string cshost { get; set; }
    public int scstatus { get; set; }
    public int scsubstatus { get; set; }
    public string scwin32status { get; set; }
    public string scbytes { get; set; }
    public string csbytes { get; set; }
    public string timetaken { get; set; }
    public string streamid { get; set; }
}