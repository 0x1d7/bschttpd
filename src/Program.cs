using System.IO.Compression;
using System.Net;
using bschttpd;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        var env = hostingContext.HostingEnvironment;
        
        config.AddJsonFile("appsettings.json", false, false)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", false, false);
        config.AddJsonFile("contenttypes.json", false, false);
        config.AddJsonFile("excludedfilesfromcache.json", false, false);
        config.AddJsonFile("excludedfilesfromserving.json", false, false);
    })
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.Configure(app =>
        {
            var services = app.ApplicationServices;
            var memoryCache = services.GetRequiredService<IMemoryCache>();
            var logger = services.GetRequiredService<SqliteLogger>();
            var configuration = services.GetRequiredService<IConfiguration>();
            var wwwroot = configuration.GetValue<string>("WebServerConfiguration:Wwwroot");

            webBuilder.UseKestrel((context, options) =>
            {
                var config = context.Configuration;

                options.Configure(config.GetSection("Kestrel"));
            });
            
            if (wwwroot is null && !Path.Exists(wwwroot))
            {
                Console.WriteLine("Error: Wwwroot value is null or path does not exist. Halting execution.");
                Environment.Exit(1);
            }
            
            webBuilder.UseContentRoot(wwwroot);
            
            app.UseResponseCompression();
            
            var contentTypes = configuration.GetSection("ContentTypes").Get<Dictionary<string, string>>();

            if (contentTypes is null)
            {
                Console.WriteLine("Error: contentTypes is null. Halting execution.");
                Environment.Exit(1);
            }

            var staticFileProvider = new FileExtensionContentTypeProvider();

            foreach (var contentType in contentTypes)
            {
                staticFileProvider.Mappings[contentType.Key] = contentType.Value;
            }
            
            var options = new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=31536000");
                },
                DefaultContentType = "application/octet-stream",
                ServeUnknownFileTypes = true,
                HttpsCompression = HttpsCompressionMode.Compress,
                ContentTypeProvider = staticFileProvider,
                FileProvider = new PhysicalFileProvider(wwwroot)
            };

            app.UseStaticFiles(options);

            var defaultDocument = configuration.GetValue<string>("WebServerConfiguration:defaultDocument");

            if (defaultDocument is null)
            {
                Console.WriteLine("Error: defaultDocument is null. Halting execution.");
                Environment.Exit(1);
            }

            var excludedFilesFromServing = configuration.GetSection("NoServe").Get<List<string>>();

            if (excludedFilesFromServing is null)
            {
                Console.WriteLine("Error: excludedFilesFromServing is null. Halting execution.");
                Environment.Exit(1);
            }
            
            var excludedFilesFromCache = configuration.GetSection("NoCache").Get<List<string>>();

            if (excludedFilesFromCache is null)
            {
                Console.WriteLine("Error: excludedFilesFromCache is null. Halting execution.");
                Environment.Exit(1);
            }
            
            var caching = new Caching(logger);
            caching.PreCacheFiles(wwwroot, memoryCache, excludedFilesFromCache, defaultDocument);

            if(configuration.GetValue<bool>("WebServerConfiguration:HttpsRedirection"))
                app.UseHttpsRedirection();
            
            app.UseMiddleware<ResponseHeaders>();
            app.UseMiddleware<Metrics>();
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.Map("/{*url}", async context =>
                {
                    var path = context.Request.Path.Value?.TrimStart('/').TrimEnd();

                    if (path is null)
                        return; //Unknown error

                    if (context.Request.Method != HttpMethods.Get && context.Request.Method != HttpMethods.Head)
                    {
                        if (context.Response.HasStarted) return;

                        context.Response.StatusCode = (int)HttpStatusCode.NotImplemented;
                        await context.Response.WriteAsync("<h1>501 - Not Implemented</h1>");
                        return;
                    }
                    
                    if (RequestValidator.IsExcluded(path, excludedFilesFromCache))
                    {
                        if (context.Response.HasStarted) return;

                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        await context.Response.WriteAsync("<h1>404 - Not Found</h1>");
                        return;
                    }
                    
                    if (string.IsNullOrEmpty(path) || path == "/")
                    {
                        path = defaultDocument;
                        
                    }
                    
                    var fullPath = Path.Combine(wwwroot, path);
                    
                    if (memoryCache.TryGetValue(fullPath, out byte[]? fileContent))
                    {
                        context.Response.ContentType = ContentType.GetContentType(fullPath, contentTypes);
                        await context.Response.Body.WriteAsync(fileContent);
                    }
                    else if (File.Exists(fullPath))
                    {
                        var fileBytes = await File.ReadAllBytesAsync(fullPath);
                        context.Response.ContentType = ContentType.GetContentType(fullPath, contentTypes);
                        await context.Response.Body.WriteAsync(fileBytes);

                        memoryCache.Set(fullPath, fileBytes, new MemoryCacheEntryOptions
                        {
                            Priority = CacheItemPriority.NeverRemove
                        });
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        await context.Response.WriteAsync("<h1>404 - File Not Found</h1>");
                    }
                });
            });
        });
    })
    .ConfigureServices(services =>
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<GzipCompressionProvider>();
        });
        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
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
    .Build();

await host.RunAsync();