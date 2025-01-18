## Basic Httpd

This basic httpd server is built off of the cross-platform [Kestrel web server in ASP.NET](https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel).

It provides basic functionality, including HTTP as well as HTTPS support, HTTP/1.1, HTTP/2.0, and HTTP/3.0 support. It is cross-platform and should run on Windows, Linux, and macOS (macOS does not have HTTP/3 support).

# Should I use this?

Probably not.

## Requirements

To run, install the .NET 8.0 Runtime. To build, install the .NET 8.0.405 SDK and restore the Nuget packages.

[.NET 8.0 Downloads](https://dotnet.microsoft.com/download/dotnet/8.0)

## Features

* Http and Https support
* HTTP/1.1, HTTP/2.0, and HTTP/3.0 support
* Http to Https Redirection
* W3C-formatted logs output to a Sqlite database
* Status and error logs output to a Sqlite database
* In-memory caching for all served files

## Configuration

* `appsettings.json` - The primary configuration file specifying server endpoints, certificate, wwwroot, and other server-wide variables.
* `contenttypes.json` - A content type (MIME type) mapping. Many common types are already added. `application/octet-stream` is the global fallback for any missing type.
* `excludedfilesfromcache.json` - File names and types to not add to the In-Memory cache on server startup.
* `excludedfilesfromserving.json` - File names and types to not serve if requested from a web browser.

## Basic Configuration

To create a basic configuration, only `appsettings.json` must be modified. Specify the `Http` endpoint to use in the format of `http://*:8080`. This will tell the server to listen on tcp/8080 for any IP or valid hostname/fully-qualified domain name for the server.

Change `Wwwroot` to a valid path, for example, `/srv/wwwroot` on Linux, or `/Users/username/Sites/wwwroot` on macOS, or `C:\wwwroot` on Windows. Change your `DefaultDocument` to the appropriate value for a file present within the `Wwwroot` directory.

The `Wwwroot` path must be specified as an absolute path.

Verify that the included Sqlite database is in the `sql` subdirectory, for example, `/srv/wwwroot/sql/bschttpd.db`. The database can be moved outside of this default path, however the web server will need read/write access to the database and the `DefaultConnection` value would need to be adjusted.

## TLS Configuration

The TLS certificate for an Https endpoint must be a separate public/private keypair. The key file must be unencrypted and should be secured via file system ACLs. The web server must have read rights to both files. On most Linux distributions, this would mean setting the path to `/etc/ssl/certs/myCertName.pem` for the public key and `/etc/ssl/private/myCertName.key` for the private certificate. Both certificate files should be in PEM format.

Certificate file paths must be specified as absolute paths.