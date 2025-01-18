namespace bschttpd;

public static class ContentType
{
    public static string GetContentType(string path, Dictionary<string, string> contentTypes)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return contentTypes.GetValueOrDefault(extension, "application/octet-stream");
    }
}