// KestrelConfiguration.cs
namespace bschttpd.Properties
{
    public class Kestrel
    {
        public Endpoints? Endpoints { get; set; }
    }

    public class Endpoints
    {
        public Http? Http { get; set; }
        public Https? Https { get; set; }
    }

    public class Http
    {
        public string? Url { get; set; }
        public string? Protocols { get; set; }
    }

    public class Https
    {
        public string? Url { get; set; }
        public string? Protocols { get; set; }
        public Certificate? Certificate { get; set; }
        public List<string>? AdditionalHttpsHosts { get; set; }
    }

    public class Certificate
    {
        public string? Path { get; set; }
        public string? KeyPath { get; set; }
    }
}