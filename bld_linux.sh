#!/bin/sh

NET_VER="net8.0"
ARCH=$(uname -m)

if [ "$ARCH" = "aarch64" ]; then
  ARCH="arm64"
elif [ "$ARCH" != "x86_64" ]; then
  echo "Platform $ARCH not supported"
  exit 1
fi

dotnet clean
dotnet publish -c Release -r linux-$ARCH
ls -la bin/Release/$NET_VER/linux-$ARCH/publish/