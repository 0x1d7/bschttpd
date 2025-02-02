using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using bschttpd;
using bschttpd.Extensions;
using bschttpd.Properties;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
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
        
        services.AddRequestTimeouts(options => {
            options.DefaultPolicy = new RequestTimeoutPolicy {
                Timeout = TimeSpan.FromMilliseconds(5000),
                TimeoutStatusCode = 408,
                WriteTimeoutResponse = async (HttpContext context) => {
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync("Response Output");
                }
            };
        });

        if (webServerConfigurationOptions.HstsEnabled)
        {
                    services.AddHsts(options =>
                    {
                        options.MaxAge = TimeSpan.FromDays(webServerConfigurationOptions.HstsMaxAge);
                        options.IncludeSubDomains = true;
                        options.Preload = true;
                        //localhost will only work on HTTPS
                        options.ExcludedHosts.Clear(); 
                        options.ExcludedHosts.Add("localhost");
                    });
        }
        
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
                var kestrelConfig = new Kestrel();
                context.Configuration.GetSection("Kestrel").Bind(kestrelConfig);
                var config = context.Configuration;
                //will reload endpoints on cert update
                options.Configure(config.GetSection("Kestrel"), true);

                if (kestrelConfig.Endpoints.Https.AdditionalHttpsHosts == null) return;
                foreach (var url in kestrelConfig.Endpoints.Https.AdditionalHttpsHosts)
                {
                    options.ListenAnyIP(new Uri(url).Port, listenOptions =>
                    {
                        listenOptions.Protocols = Enum.Parse<HttpProtocols>(kestrelConfig.Endpoints.Https.Protocols!, 
                            ignoreCase: true);
                        listenOptions.UseHttps(httpsOptions =>
                        {
                            httpsOptions.ServerCertificate = X509Certificate2.CreateFromPemFile(
                                kestrelConfig.Endpoints.Https.Certificate.Path!,
                                kestrelConfig.Endpoints.Https.Certificate.KeyPath);
                        });
                    });
                }
            });
            
            ServerLog.KestrelConfigured(logger);

            var contentTypeProvider = new FileExtensionContentTypeProvider();
            var physicalFileProvider = new PhysicalFileProvider(wwwroot);

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

            if (webServerConfigurationOptions.DirectoryBrowsingEnabled)
            {
                var directoryPhysicalFileProvider = 
                    new PhysicalFileProvider(webServerConfigurationOptions.DirectoryBrowserPath);
                
                app.UseDirectoryBrowser(new DirectoryBrowserOptions
                {
                    FileProvider = directoryPhysicalFileProvider
                });
            }
            
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

            if (webServerConfigurationOptions.HstsEnabled)
            {
                app.UseHsts();
                app.UseHttpsRedirection(); //ignore admin setting
                
                ServerLog.HstsEnabled(logger);
            }
            
            /* Ordered to apply custom headers first, logging before response,
                response error checking before serving */

            app.UseHostFiltering();
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