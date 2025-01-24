using Microsoft.AspNetCore.StaticFiles;

namespace bschttpd;

public static class ContentType
{
    internal static string? GetContentType(string path)
    {
        var contentTypeProvider = new FileExtensionContentTypeProvider();
        
        if (!contentTypeProvider.TryGetContentType(path, out string? contentType))
        {
            contentType = "application/octet-stream";
        }
        
        return contentType;
    }

}