using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Runtime.CompilerServices;
using bschttpd;
using bschttpd.Properties;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#pragma warning disable CS8602 // Dereference of a possibly null reference.

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        var env = hostingContext.HostingEnvironment;
        
        config.AddJsonFile("appsettings.json", false, false)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", false, false);
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
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var contentConfiguration = hostingContext.Configuration.GetSection(nameof(ContentConfiguration));

        services.AddOptions<ContentConfiguration>()
            .BindConfiguration(contentConfiguration.Path, bindOptions =>
            {
                bindOptions.ErrorOnUnknownConfiguration = true;
            })
            .ValidateOnStart();

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<GzipCompressionProvider>();
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

        services.AddMemoryCache();
        services.AddSingleton<SqliteConnection>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (connectionString is null) 
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found."); 
            return new SqliteConnection(connectionString);
        });
        services.AddSingleton<SqliteLogger>(provider =>
        {
            var connection = provider.GetRequiredService<SqliteConnection>();
            var categoryName = "DefaultCategory";
            return new SqliteLogger(connection, categoryName);
        });
        
        services.AddSingleton<Caching>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        
        LoggerFactory.Create(builder => builder.AddConsole());
    })
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.Configure((hostingContext, app) =>
        {
            var services = app.ApplicationServices;
            var memoryCache = services.GetRequiredService<IMemoryCache>();
            var logger = services.GetRequiredService<SqliteLogger>();
            var configuration = services.GetRequiredService<IConfiguration>();
            var webServerConfiguration = configuration.GetRequiredSection(nameof(WebServerConfiguration));
            var webServerConfigurationOptions = webServerConfiguration.Get<WebServerConfiguration>();
            var contentConfiguration = hostingContext.Configuration.GetRequiredSection(nameof(ContentConfiguration));
            var contentConfigurationOptions = contentConfiguration.Get<ContentConfiguration>();
            var wwwroot = webServerConfigurationOptions.Wwwroot;
            
            webBuilder.UseKestrel((context, options) =>
            {
                var config = context.Configuration;

                options.Configure(config.GetSection("Kestrel"));
            });
            
            webBuilder.UseContentRoot(wwwroot);
            
            app.UseResponseCompression();
            app.UseW3CLogging();
            
            var staticFileProvider = new FileExtensionContentTypeProvider();
            
            foreach (var contentType in contentConfigurationOptions.ContentTypeMap)
            {
                staticFileProvider.Mappings[contentType.Key] = contentType.Value;
            }

            var options = new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                   ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=31536000");
                   ctx.Context.Response.OnStarting(() => Task.CompletedTask);
                },
                DefaultContentType = "application/octet-stream",
                ServeUnknownFileTypes = true,
                HttpsCompression = HttpsCompressionMode.Compress,
                ContentTypeProvider = staticFileProvider,
                FileProvider = new PhysicalFileProvider(wwwroot)
            };

            app.UseStaticFiles(options);
            
            var caching = new Caching(logger);
            caching.PreCacheFiles(wwwroot, memoryCache, contentConfigurationOptions.NoCache, webServerConfigurationOptions.DefaultDocument);

            if(webServerConfigurationOptions.HttpsRedirection)
                app.UseHttpsRedirection();
            
            app.UseMiddleware<ResponseHeadersMiddleware>();
            app.UseMiddleware<RequestHandlingMiddleware>();
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
                endpoints.Map("/{*url}", async context =>
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
                {
                });
            });
        });
    })
    .Build();

await host.RunAsync();