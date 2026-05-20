#!/usr/bin/env bash
set -e

# Restore and publish native Linux executables for x64 and ARM64.
rm ./publish -rf
#dotnet restore

dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/linux-x64

# dotnet publish -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true -o ./publish/linux-arm64

# dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/win-x64
./publish/linux-x64/idk2