## BasicHttpd

This BasicHttpd server is built off of the cross-platform [Kestrel web server in ASP.NET](https://learn.microsoft.
com/aspnet/core/fundamentals/servers/kestrel).

It provides basic functionality, including HTTP as well as HTTPS support, HTTP/1.1, HTTP/2.0, and HTTP/3.0 support. 
It is cross-platform and should run on Windows, Linux, and macOS.

# Should I use this?

Probably not.

## Requirements

To run, install the .NET 8.0 ASP.NET Core Runtime. To build, install the .NET 8.0.405 SDK and restore the Nuget 
packages.

[.NET 8.0 Downloads](https://dotnet.microsoft.com/download/dotnet/8.0)

This application has been tested on:

* macOS 15.2 (arm64)
* Windows 11 (x86-64)
* Ubuntu 24.04 (arm64)

## Features

* Http and Https support
* HTTP/1.1, HTTP/2.0, and HTTP/3.0 support
* Http to Https Redirection
* W3C-formatted logs output to a Sqlite database
* Status and error logs output to a Sqlite database
* In-memory caching for all served files

## Deployment

Build BasicHttpd in release mode. Copy the files to a dedicated directory. BasicHttpd must have write access to the 
file `bschttpd.db` but otherwise only requires read access to all other files. The default directory structure is as 
follows.

```text
./<binaries>
./errorpages/<status_code>.html
./sql/bschttpd.db
```

## Configuration

`appsettings.config` contains all configuration elements. The basic setup requires modifying the `Kestrel` element 
appropriately. Remove either protocol element should it not be desired. Valid values for `Protocols` are:

### Protocols

The first step to configuring BasicHttpd is to determine the appropriate protocols to use. For the `Http` element, 
the only valid protocol is `Http1`. The `Https` element supports the following protocols.

* Http1
* Http2
* Http3
* Http1AndHttp2
* Http1AndHttp2AndHttp3

Http2 and Http3, or combinations there of, require an SSL certificate for the `Https` element. Note that .NET does 
not have Http3 support in macOS at this time.

### TLS Configuration

`Certificate` has two elements, `Path` and `KeyPath`. The SSL certificate must have an unencrypted key. This can 
be accomplished via OpenSSL for a key that is encrypted or a format like p12 or pfx; search the web for "OpenSSL 
convert encrypted to pem". Note the file extension of the resulting certificate files does not matter, but typically 
the public key (for the `Path` element) is .pem while the private key file (for `KeyPath`) is .key.

The public key when opened with a text editor will have this header and footer:

```text
-----BEGIN CERTIFICATE-----
<Random characters>
-----END CERTIFICATE-----
```
A valid unencrypted private key will have this as the header:

```text
-----BEGIN PRIVATE KEY-----
<Random characters>
-----END PRIVATE KEY-----
```
Because the private key is unencrypted it should be located in a directory where the web server can read it but no 
other user can, meaning the web server should be run under a user account dedicated to BasicHttpd.

The `Url` is currently only used for the protocol value (`http://` and `https://`) as well as the port number. Note 
that port numbers below port 1024 generally require elevated rights on operating systems.

### WWW Content Directory

The next step is to specify the `Wwwroot` value. This value is where the files you want to serve via the web are 
located. It must be a fully qualified path. Examples of this might be `/srv/wwwroot` on Linux, 
`/Users/userName/Sites/wwwroot` on macOS, or `C:\basichttpd\wwwroot` on Windows. If running BasicHttpd under a 
dedicated user account, the user must have read only rights to this directory.

### Other Settings

These settings may be freely modified provided you follow the format of the existing examples.

`DefaultDocument` is the default file that will be served when a client requests the root of your website, i.e. 
`https://localhost:443`.

`NoServe` is a list of filenames and file extensions which will not be served by the web server when requested by a 
client.

`NoCache` is a list of filenames and file extensions which will not be cached by BasicHttpd during the lifecycle of 
the application.

`ContentTypeMap` is a list of MIME type mappings.

`Logging` is not used at runtime.