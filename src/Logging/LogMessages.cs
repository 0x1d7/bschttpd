using System;
using Microsoft.Extensions.Logging;

namespace bschttpd
{
    public static partial class ServerLog
    {
        [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Web server configured: {wwwroot}")]
        public static partial void WebServerConfigured(ILogger logger, string wwwroot);
        [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Kestrel configured.")]
        public static partial void KestrelConfigured(ILogger logger);
        
        [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "PhysicalFileProvider configured.")]
        public static partial void PhysicalFileProviderConfigured(ILogger logger);
        
        [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "DefaultFilesOptions configured. Default document is {defaultDocument}.")]
        public static partial void DefaultFilesOptionsConfigured(ILogger logger, string defaultDocument);
        
        [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "StaticFileOptions configured.")]
        public static partial void StaticFileOptionsConfigured(ILogger logger);
        
        [LoggerMessage(EventId = 1005, Level = LogLevel.Information, Message = "Https redirect {httpsredirect}.")]
        public static partial void HttpsRedirectConfigured(ILogger logger, bool httpsRedirect);
        [LoggerMessage(EventId = 1006, Level = LogLevel.Information, Message = "Content root: {contentRoot}.")]
        public static partial void ContentRoot(ILogger logger, string contentRoot);
        
        [LoggerMessage(EventId = 1007, Level = LogLevel.Information, Message = "Middleware configured.")]
        public static partial void MiddlewareConfigured(ILogger logger);

        [LoggerMessage(EventId = 1008, Level = LogLevel.Information, Message = "Hsts enabled.")]
        public static partial void HstsEnabled(ILogger logger);
        
        [LoggerMessage(EventId = 5000, Level = LogLevel.Error, Message = "Exception occurred.")]
        public static partial void ExceptionOccurred(ILogger logger, Exception exception);
        
        [LoggerMessage(EventId = 5001, Level = LogLevel.Error, Message = "Error flushing logs.")]
        public static partial void ErrorFlushingLogs(ILogger logger, Exception exception);
    }
}