@echo off
REM Publish M2M with Native AOT for maximum performance
REM Requires .NET 9 SDK and Visual Studio 2022 with C++ tools (Desktop development with C++ workload)

echo Building M2M with Native AOT...

REM Clean previous builds
dotnet clean -c Release

REM Publish with AOT
REM -c Release: Release configuration
REM -r win-x64: Target Windows x64 platform
REM --self-contained: Self-contained (includes runtime)
REM -p:PublishAot=true: Enable Native AOT compilation
dotnet publish -c Release -r win-x64 --self-contained -p:PublishAot=true

echo.
echo Done! Native executable is at: src\Msys2Manager.CLI\bin\Release\net9.0\win-x64\publish\m2m.exe
pause
