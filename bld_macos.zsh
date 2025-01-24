#!/bin/zsh

NET_VER="net8.0"
ARCH=$(uname -m)

dotnet clean
dotnet publish -c Release -r osx-$ARCH && strip bin/Release/$NET_VER/osx-$ARCH/publish/bschttpd
ls -la bin/Release/$NET_VER/osx-$ARCH/publish/