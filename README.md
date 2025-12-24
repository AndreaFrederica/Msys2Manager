# M2M - MSYS2 Management Tool

A modern C# command-line tool for managing MSYS2 environments on Windows.

## Features

- **Bootstrap**: Initialize MSYS2 environment with a single command
- **Package Management**: Add, remove, and sync packages with TOML configuration
- **Task Runner**: Execute predefined tasks from configuration
- **Interactive Shell**: Launch MSYS2 shell with correct environment
- **Clean Uninstall**: Remove MSYS2 environment when needed

## Installation

### Build from source

```bash
dotnet build src/Msys2Manager.CLI/Msys2Manager.CLI.csproj -c Release
```

The compiled `m2m.exe` will be in `src/Msys2Manager.CLI/bin/Release/net9.0/`.

### As a global tool

```bash
dotnet pack src/Msys2Manager.CLI/Msys2Manager.CLI.csproj -c Release
dotnet tool install --global --add-source ./src/Msys2Manager.CLI/bin/Release/ Msys2Manager.CLI
```

## Usage

```bash
# Initialize MSYS2 environment
m2m bootstrap

# Update all packages
m2m update

# Sync packages with configuration
m2m sync --prune

# Add a package
m2m add mingw-w64-ucrt-x86_64-cmake

# Remove a package from configuration
m2m remove mingw-w64-ucrt-x86_64-cmake

# Run a task
m2m run build

# List available tasks
m2m run --list

# Start interactive shell
m2m shell

# Remove MSYS2 environment
m2m clean --force
```

## Configuration

Create `msys2.toml` in your project root:

```toml
[msys2]
msystem = "UCRT64"
base_url = "https://github.com/msys2/msys2-installer/releases/download/2024-01-13/"
mirror = "https://mirror.msys2.org/"
auto_update = true

[packages]
mingw-w64-ucrt-x86_64-toolchain = "*"
mingw-w64-ucrt-x86_64-cmake = "*"
mingw-w64-ucrt-x86_64-ninja = "*"

[tasks]
configure = "cmake -S . -B build -G Ninja"
build = "cmake --build build --parallel"
```

## Requirements

- .NET 9.0 Runtime
- Windows 10+ (for MSYS2)
- Internet connection (for initial bootstrap)

## License

LGPL-3.0-only (Same as Prism Launcher)

## Contributing

This project is part of the Prism Launcher ecosystem. Contributions are welcome!
