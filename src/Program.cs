using System.IO.Compression;
using bschttpd;
using bschttpd.Extensions;
using bschttpd.Properties;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpLogging;
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
        config.AddJsonFile(Path.Combine(basePath,$"appsettings.json"), false, false)
            .AddJsonFile(Path.Combine(basePath,$"appsettings.{env.EnvironmentName}.json"), false, false);

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

        services.AddW3CLogging(logging =>
        {
            logging.LoggingFields = W3CLoggingFields.All;
            logging.FlushInterval = TimeSpan.FromSeconds(webServerConfigurationOptions.W3CLogFlushInterval);
            logging.FileSizeLimit = webServerConfigurationOptions.W3CLogFileSizeLimit;
            logging.FileName = webServerConfigurationOptions.W3CLogName;
            logging.LogDirectory = webServerConfigurationOptions.W3CLogDirectory;
        });

        services.AddCustomHostFiltering(hostingContext);
        
        services.AddMemoryCache();
        services.AddResponseCaching();
        services.AddDirectoryBrowser();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        
        var logsDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

        if (!Directory.Exists(logsDirectory))
        {
            try
            {
                Directory.CreateDirectory(logsDirectory);
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"Unable to create logs directory: {logsDirectory}.");
            }
        }
   
        var errorFilePath = Path.Combine(logsDirectory, "errors.log");
        var statusFilePath = Path.Combine(logsDirectory, "status.log");
        
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
            Log.WebServerConfigured(logger, wwwroot);
            
            webBuilder.UseKestrel((context, options) =>
            {
                var config = context.Configuration;
                //will reload endpoints on cert update
                options.Configure(config.GetSection("Kestrel"), true);
            });
            
            Log.KestrelConfigured(logger);

            var contentTypeProvider = new FileExtensionContentTypeProvider();
            var physicalFileProvider = new PhysicalFileProvider(wwwroot);
            var directoryPhysicalFileProvider = new PhysicalFileProvider(Path.Combine(
                webServerConfigurationOptions.Wwwroot,
                webServerConfigurationOptions.DirectoryBrowserRelativeDefaultPath));

            Log.PhysicalFileProviderConfigured(logger);
            
            var defaultFileOptions = new DefaultFilesOptions();
            defaultFileOptions.DefaultFileNames.Clear();
            defaultFileOptions.DefaultFileNames.Add(webServerConfigurationOptions.DefaultDocument);
            defaultFileOptions.FileProvider = physicalFileProvider;

            Log.DefaultFilesOptionsConfigured(logger, webServerConfigurationOptions.DefaultDocument);
            
            var staticFileOptions = new StaticFileOptions
            {
                DefaultContentType = "application/octet-stream",
                ServeUnknownFileTypes = true,
                HttpsCompression = HttpsCompressionMode.Compress,
                ContentTypeProvider = contentTypeProvider,
                FileProvider = physicalFileProvider,
                
            };

            Log.StaticFileOptionsConfigured(logger);
            
            app.UseDirectoryBrowser(new DirectoryBrowserOptions
            {
                FileProvider = directoryPhysicalFileProvider,
                RequestPath = Path.Combine("/", 
                    webServerConfigurationOptions.DirectoryBrowserRelativeDefaultPath)
            });

            if (webServerConfigurationOptions.HttpsRedirection)
            {
                app.UseHttpsRedirection();
                Log.HttpsRedirectConfigured(logger, webServerConfigurationOptions.HttpsRedirection);
            }
            else
            {
                Log.HttpsRedirectConfigured(logger, webServerConfigurationOptions.HttpsRedirection);
            }
            
            webBuilder.UseContentRoot(wwwroot);
            
            Log.ContentRoot(logger, wwwroot);
            
            /* Ordered to apply custom headers first, logging before response,
                response error checking before serving */
            app.UseMiddleware<ResponseHeadersMiddleware>();
            app.UseResponseCaching();
            app.UseResponseCompression();
            app.UseW3CLogging();
            app.UseMiddleware<RequestHandlingMiddleware>();
            app.UseDefaultFiles(defaultFileOptions);
            app.UseStaticFiles(staticFileOptions); //move to MapStaticAssets in 10.0
            
            Log.MiddlewareConfigured(logger);
            
            app.UseRouting();
        });
    })
    .Build();

await host.RunAsync();