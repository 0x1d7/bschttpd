using bschttpd.Properties;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace bschttpd;

public class RequestHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<WebServerConfiguration> _webServerConfig;
    private readonly IOptions<ContentConfiguration> _contentConfig;
    
    public RequestHandlingMiddleware(RequestDelegate next, IMemoryCache memoryCache, IOptions<WebServerConfiguration> webServerConfiguration,
        IOptions<ContentConfiguration> contentConfiguration)
    {
        _next = next;
        _webServerConfig = webServerConfiguration;
        _contentConfig = contentConfiguration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.TrimStart('/').TrimEnd();
       
        if (path is null)
        {
            //Unknown error, shouldn't happen
            if (context.Response.HasStarted) return;
            await HandleErrorResponse(context, 400);
            return;
        }

        if (context.Request.Method != HttpMethods.Get && context.Request.Method != HttpMethods.Head)
        {
            if (context.Response.HasStarted) return;
            await HandleErrorResponse(context, 501);
            return;
        }

        if (IsExcluded(path, _contentConfig.Value.NoServe))
        {
            if (context.Response.HasStarted) return;
            await HandleErrorResponse(context, 404);
            return;
        }
        
        if (Path.GetFileName(path).StartsWith('.'))
        {
            if (context.Response.HasStarted) return;
            await HandleErrorResponse(context, 404);
            return;
        }
        
        await _next(context);
    }

    private async Task HandleErrorResponse(HttpContext context, int statusCode)
    {
        var errorPath = Path.Combine($"{System.Environment.CurrentDirectory}/{_webServerConfig.Value.ErrorPagesPath}", 
            $"{statusCode}.html");
        if (File.Exists(errorPath))
        {
            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(await File.ReadAllTextAsync(errorPath));
        }
        else
        {
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsync($"An error occurred: {statusCode}");
        }

        //short circuit remaining middleware
        await context.Response.CompleteAsync();
    }

    private bool IsExcluded(string path, List<string> noServe)
    {
        var fileName = Path.GetFileName(path);
        return fileName.StartsWith(".") ||
               noServe.Any(ex => path.EndsWith(ex, StringComparison.OrdinalIgnoreCase));
    }
}