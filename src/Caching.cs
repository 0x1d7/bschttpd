using Microsoft.Extensions.Caching.Memory;

namespace bschttpd;

public class Caching(SqliteLogger logger)
{
    public void PreCacheFiles(string wwwRoot, IMemoryCache memoryCache, List<string> excludedFiles,
        string defaultDocument)
    {
        var files = Directory.GetFiles(wwwRoot, "*.*", SearchOption.AllDirectories);

        var defaultDocumentContent = File.ReadAllBytes(Path.Combine(wwwRoot, defaultDocument));
        memoryCache.Set(defaultDocument, defaultDocumentContent);

        foreach (var file in files)
        {
            if (!excludedFiles.Any(ex => file.EndsWith(ex, StringComparison.OrdinalIgnoreCase))
                && !Path.GetFileName(file).StartsWith("."))
            {
                var fileContent = File.ReadAllBytes(file);
                memoryCache.Set(file, fileContent, new MemoryCacheEntryOptions
                {
                    Priority = CacheItemPriority.NeverRemove
                });

                logger.LogStatus($"File {file} has been pre-cached");
                Console.WriteLine($"File {file} has been pre-cached");
            }
        }
    }
}