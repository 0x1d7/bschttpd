using Microsoft.AspNetCore.Http;

namespace bschttpd;

public class RequestValidator
{
    public static bool IsExcluded(string path, List<string> excludedFiles)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (Path.GetFileName(path).StartsWith('.'))
        {
            return true;
        }
    
        return excludedFiles.Any(ex => path.EndsWith(ex, StringComparison.OrdinalIgnoreCase));
    }
}