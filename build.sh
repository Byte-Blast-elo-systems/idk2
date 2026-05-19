#!/usr/bin/env bash
set -e

# Restore and publish native Linux executables for x64 and ARM64.
# Add --self-contained true so the outputs are native executables.

dotnet restore

dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/linux-x64

dotnet publish -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true -o ./publish/linux-arm64
