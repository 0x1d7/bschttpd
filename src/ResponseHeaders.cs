using Microsoft.AspNetCore.Http;

namespace bschttpd;

public class ResponseHeaders
{
    private readonly RequestDelegate _next;

    public ResponseHeaders(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Server"] = "Basic-HTTPd/1.0";
            return Task.CompletedTask;
        });
        
        await _next(context);
    }
}