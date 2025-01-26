using System;
using System.Threading.Tasks;
using bschttpd.Properties;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace bschttpd;

public class ResponseHeadersMiddleware(RequestDelegate next, IOptions<WebServerConfiguration> webServerConfiguration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Server = webServerConfiguration.Value.ServerName;
            context.Response.GetTypedHeaders().CacheControl = 
                new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
            {
                Public = true,
                MaxAge = TimeSpan.FromSeconds(webServerConfiguration.Value.CacheControlMaxAge)
            };
            return Task.CompletedTask;
        });
        
        await next(context);
    }
}