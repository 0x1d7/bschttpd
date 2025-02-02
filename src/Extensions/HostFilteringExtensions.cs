using bschttpd.Properties;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace bschttpd.Extensions
{
    public static class HostFilteringExtensions
    {
        public static IServiceCollection AddCustomHostFiltering(this IServiceCollection services, HostBuilderContext hostingContext)
        {
            // Get Kestrel configuration section using strong typing
            var kestrelConfiguration = hostingContext.Configuration.GetRequiredSection(nameof(Kestrel));
            var kestrelConfigurationOptions = kestrelConfiguration.Get<Kestrel>();

            var allowedHosts = new List<string>();

            // Extract hosts from Kestrel Endpoints
            if (kestrelConfigurationOptions?.Endpoints != null)
            {
                var httpEndpoint = kestrelConfigurationOptions.Endpoints.Http;
                var httpsEndpoint = kestrelConfigurationOptions.Endpoints.Https;

                if (!string.IsNullOrEmpty(httpEndpoint?.Url))
                {
                    var url = httpEndpoint.Url;

                    if (url!.Contains('*'))
                    {
                        allowedHosts.Add("*");
                    }
                    else
                    {
                        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                        {
                            var host = uri.Host;
                            if (!allowedHosts.Contains(host))
                            {
                                allowedHosts.Add(host);
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(httpsEndpoint?.Url))
                {
                    var url = httpsEndpoint.Url;

                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        var host = uri.Host;
                        if (!allowedHosts.Contains(host))
                        {
                            allowedHosts.Add(host);
                        }
                    }
                }
            }

            if (kestrelConfigurationOptions?.Endpoints?.Https?.AdditionalHttpsHosts != null)
            {
                foreach (var additionalHttpsHost in 
                         kestrelConfigurationOptions.Endpoints.Https.AdditionalHttpsHosts)
                {
                    var url = additionalHttpsHost;
                    
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        var host = uri.Host;
                        if (!allowedHosts.Contains(host))
                        {
                            allowedHosts.Add(host);
                        }
                    }
                }
            }

            services.AddHostFiltering(options =>
            {
                options.AllowedHosts = allowedHosts.ToArray();
            });

            return services;
        }
    }
}