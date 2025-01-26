using System.IO.Compression;
using bschttpd;
using bschttpd.Extensions;
using bschttpd.Properties;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

#pragma warning disable CS8602 // Dereference of a possibly null reference.

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        var env = hostingContext.HostingEnvironment;
        var basePath = AppContext.BaseDirectory;
        config.AddJsonFile(Path.Combine(basePath,$"appsettings.{env.EnvironmentName}.json"), false, false);

        if (env.IsDevelopment())
        {
            config.AddJsonFile(Path.Combine(basePath,$"appsettings.Development.local.json"), false, false);
        }
    })
    .ConfigureServices((hostingContext, services) =>
    {
        var webServerConfiguration = hostingContext.Configuration.GetSection(nameof(WebServerConfiguration));
        var webServerConfigurationOptions = webServerConfiguration.Get<WebServerConfiguration>();
        
        services.AddOptions<WebServerConfiguration>()
            .BindConfiguration(webServerConfiguration.Path, bindOptions =>
            {
                bindOptions.ErrorOnUnknownConfiguration = true;
            })
            .ValidateOnStart();

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = false;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });
        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });
        
        services.AddCustomHostFiltering(hostingContext);
        services.AddMemoryCache();
        services.AddResponseCaching();
        services.AddDirectoryBrowser();
    })
    .ConfigureLogging((hostingContext, logging) =>
    {
        var webServerConfiguration = hostingContext.Configuration.GetSection(nameof(WebServerConfiguration));
        var webServerConfigurationOptions = webServerConfiguration.Get<WebServerConfiguration>();
        
        logging.ClearProviders();
   
        var errorFilePath = Path.Combine(webServerConfigurationOptions.W3CLogDirectory, "errors.log");
        var statusFilePath = Path.Combine(webServerConfigurationOptions.W3CLogDirectory, "status.log");
        
        logging.AddProvider(new FileLoggingProviderProvider(errorFilePath, statusFilePath));
    })

    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.Configure(app =>
        {
#if WINDOWS
            webBuilder.UseWindowsService();
#endif
            
            var services = app.ApplicationServices;
            var logger = services.GetRequiredService<ILogger<Program>>();
            var configuration = services.GetRequiredService<IConfiguration>();
            var webServerConfiguration = configuration.GetRequiredSection(nameof(WebServerConfiguration));
            var webServerConfigurationOptions = webServerConfiguration.Get<WebServerConfiguration>();
            var wwwroot = webServerConfigurationOptions.Wwwroot;
            
            ServerLog.WebServerConfigured(logger, wwwroot);
            
            webBuilder.UseKestrel((context, options) =>
            {
                var config = context.Configuration;
                //will reload endpoints on cert update
                options.Configure(config.GetSection("Kestrel"), true);
            });
            
            ServerLog.KestrelConfigured(logger);

            var contentTypeProvider = new FileExtensionContentTypeProvider();
            var physicalFileProvider = new PhysicalFileProvider(wwwroot);
            var directoryPhysicalFileProvider = new PhysicalFileProvider(Path.Combine(
                webServerConfigurationOptions.Wwwroot,
                webServerConfigurationOptions.DirectoryBrowserRelativeDefaultPath));

            ServerLog.PhysicalFileProviderConfigured(logger);
            
            var defaultFileOptions = new DefaultFilesOptions();
            defaultFileOptions.DefaultFileNames.Clear();
            defaultFileOptions.DefaultFileNames.Add(webServerConfigurationOptions.DefaultDocument);
            defaultFileOptions.FileProvider = physicalFileProvider;

            ServerLog.DefaultFilesOptionsConfigured(logger, webServerConfigurationOptions.DefaultDocument);
            
            var staticFileOptions = new StaticFileOptions
            {
                DefaultContentType = "application/octet-stream",
                ServeUnknownFileTypes = true,
                HttpsCompression = HttpsCompressionMode.Compress,
                ContentTypeProvider = contentTypeProvider,
                FileProvider = physicalFileProvider,
                
            };

            ServerLog.StaticFileOptionsConfigured(logger);
            
            app.UseDirectoryBrowser(new DirectoryBrowserOptions
            {
                FileProvider = directoryPhysicalFileProvider,
                RequestPath = Path.Combine("/", 
                    webServerConfigurationOptions.DirectoryBrowserRelativeDefaultPath)
            });

            if (webServerConfigurationOptions.HttpsRedirection)
            {
                app.UseHttpsRedirection();
                ServerLog.HttpsRedirectConfigured(logger, webServerConfigurationOptions.HttpsRedirection);
            }
            else
            {
                ServerLog.HttpsRedirectConfigured(logger, webServerConfigurationOptions.HttpsRedirection);
            }
            
            webBuilder.UseContentRoot(wwwroot);
            
            ServerLog.ContentRoot(logger, wwwroot);
            
            /* Ordered to apply custom headers first, logging before response,
                response error checking before serving */
            app.UseMiddleware<ResponseHeadersMiddleware>();
            app.UseResponseCaching();
            app.UseResponseCompression();
            app.UseMiddleware<RotatingW3CLoggingMiddleware>();

            app.UseMiddleware<RequestHandlingMiddleware>();
            app.UseDefaultFiles(defaultFileOptions);
            app.UseStaticFiles(staticFileOptions); //move to MapStaticAssets in 10.0
            
            ServerLog.MiddlewareConfigured(logger);
            
            app.UseRouting();
        });
    })
    .Build();

await host.RunAsync();