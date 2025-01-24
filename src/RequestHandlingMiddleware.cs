using bschttpd.Properties;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace bschttpd;

public class RequestHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<WebServerConfiguration> _webServerConfiguration;
    private readonly IMemoryCache _memoryCache;
    public RequestHandlingMiddleware(RequestDelegate next, IOptions<WebServerConfiguration> webServerConfiguration, 
        IMemoryCache memoryCache)
    {
        _next = next;
        _webServerConfiguration = webServerConfiguration;
        _memoryCache = memoryCache;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.TrimStart('/').TrimEnd();
        var filePath = path;
        
        if (filePath == "" || filePath == "/")
        {
            filePath = _webServerConfiguration.Value.DefaultDocument;
        }

        if (filePath != null)
        {
            filePath = Path.GetFullPath(Path.Combine(_webServerConfiguration.Value.Wwwroot, filePath));

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

            if (IsExcluded(path, _webServerConfiguration.Value.NoServe))
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

            if (_memoryCache.TryGetValue(filePath, out byte[]? cacheEntry))
            {
                context.Response.ContentType = ContentType.GetContentType(filePath);
                await context.Response.Body.WriteAsync(cacheEntry);
                return;
            }

            await _next(context);

            switch (context.Response.StatusCode)
            {
                case StatusCodes.Status404NotFound when context.Response.HasStarted:
                    return;
                case StatusCodes.Status404NotFound:
                    await HandleErrorResponse(context, 404);
                    break;
                case StatusCodes.Status200OK:
                {
                    var fileInfo = new FileInfo(filePath);

                    if (fileInfo.Length < 1 * 1024 * 1024)
                    {
                        var fileContent = await File.ReadAllBytesAsync(filePath);

                        _memoryCache.Set(filePath, fileContent, new MemoryCacheEntryOptions
                        {
                            Priority = CacheItemPriority.High
                        });
                    }
                    break;
                }
            }
        }
    }

    private async Task HandleErrorResponse(HttpContext context, int statusCode)
    {
        var errorPath = Path.Combine($"{Environment.CurrentDirectory}/{_webServerConfiguration.Value.ErrorPagesPath}", 
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