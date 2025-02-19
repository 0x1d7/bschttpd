using System.ComponentModel.DataAnnotations;
// ReSharper disable InconsistentNaming
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace bschttpd.Properties;

public class WebServerConfiguration
{
    [Required]
    public bool HttpsRedirection { get; set; } = false;

    public bool HstsEnabled { get; set; } = false;

    public int HstsMaxAge { get; set; } = 60;
    [Required]
    public string Wwwroot {get; set; }
    public string DefaultDocument { get; set; } = "index.html";
    public string ErrorPagesPath { get; set; } = "errorpages";
    public int CacheControlMaxAge { get; set; } = 3600;
    public int W3CLogFlushInterval { get; set; } = 300;
    public int W3CLogFileSizeLimit { get; set; } = 52428800;
    public string W3CLogName { get; set; } = "bschttpd_w3c";
    public string W3CLogDirectory { get; set; } = "logs";
    public string ServerName => "Basic-Httpd/1.0";
    public bool DirectoryBrowsingEnabled { get; set; } = false;
    public string DirectoryBrowserPath { get; set; } = "files";
    public List<string> NoServe { get; set; }
}