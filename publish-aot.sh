#!/bin/bash
# Publish M2M with Native AOT for maximum performance
# Requires .NET 9 SDK and Visual Studio 2022 with C++ tools (on Windows)

set -e

echo "Building M2M with Native AOT..."

# Clean previous builds
dotnet clean -c Release

# Publish with AOT
# -c Release: Release configuration
# -r win-x64: Target Windows x64 platform
# --self-contained: Self-contained (includes runtime)
# -p:PublishAot=true: Enable Native AOT compilation
dotnet publish -c Release -r win-x64 --self-contained -p:PublishAot=true

echo "Done! Native executable is at: src/Msys2Manager.CLI/bin/Release/net9.0/win-x64/publish/m2m.exe"
