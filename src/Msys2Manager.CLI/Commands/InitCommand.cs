using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Configuration;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO.Abstractions;

namespace Msys2Manager.CLI.Commands;

public class InitCommand : Command
{
    public readonly Option<string?> MSystemOption;
    public readonly Option<string?> MirrorOption;
    public readonly Option<string?> VersionOption;
    public readonly Option<bool> ListVersionsOption;

    public InitCommand() : base("init", "Initialize a new MSYS2 environment in the current directory")
    {
        MSystemOption = new Option<string?>(
            ["--msystem", "-m"],
            "The MSYS2 system to use (UCRT64, CLANG64, MINGW64, MINGW32, CLANG32). If not specified, you will be prompted."
        );

        MirrorOption = new Option<string?>(
            ["--mirror"],
            "Mirror URL for package downloads"
        );

        VersionOption = new Option<string?>(
            ["--version", "-v"],
            "MSYS2 version (e.g., 2024-01-13). Defaults to latest available."
        );

        ListVersionsOption = new Option<bool>(
            ["--list-versions", "-l"],
            "List available MSYS2 versions"
        );

        AddOption(MSystemOption);
        AddOption(MirrorOption);
        AddOption(VersionOption);
        AddOption(ListVersionsOption);
    }

    public new class Handler : ICommandHandler
    {
        private readonly IFileSystem _fileSystem;
        private readonly IConfigurationService _configuration;
        private readonly IVersionService _versionService;

        public Handler(IFileSystem fileSystem, IConfigurationService configuration, IVersionService versionService)
        {
            _fileSystem = fileSystem;
            _configuration = configuration;
            _versionService = versionService;
        }

        public string? MSystem { get; set; }
        public string? Mirror { get; set; }
        public string? Version { get; set; }
        public bool ListVersions { get; set; }

        public int Invoke(InvocationContext context)
        {
            return InvokeAsync(context).GetAwaiter().GetResult();
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var console = context.Console;

            // Handle --list-versions option
            if (ListVersions)
            {
                console.Out.Write("Fetching available MSYS2 versions...\n");
                var versions = await _versionService.GetAvailableVersionsAsync(context.GetCancellationToken());

                console.Out.Write($"\nAvailable versions ({versions.Length} total):\n");
                foreach (var version in versions.Take(20)) // Show latest 20
                {
                    console.Out.Write($"  {version}\n");
                }
                if (versions.Length > 20)
                {
                    console.Out.Write($"  ... and {versions.Length - 20} more\n");
                }
                console.Out.Write($"\nLatest version: {versions.FirstOrDefault() ?? "unknown"}\n");

                return 0;
            }

            var projectRoot = _configuration.GetProjectRoot();
            var configPath = _fileSystem.Path.Combine(projectRoot, "msys2.toml");

            // Check if msys2.toml already exists
            if (_fileSystem.File.Exists(configPath))
            {
                console.Error.Write($"Error: msys2.toml already exists at {projectRoot}\n");
                console.Error.Write("To reinitialize, delete the existing file first.\n");
                return 1;
            }

            // Get version - use specified version or fetch latest
            var msys2Version = Version;
            if (string.IsNullOrEmpty(msys2Version))
            {
                console.Out.Write("Fetching latest MSYS2 version...\n");
                msys2Version = await _versionService.GetLatestVersionAsync(context.GetCancellationToken());
                console.Out.Write($"Using latest version: {msys2Version}\n");
            }

            // Create default configuration
            var config = new Msys2Configuration
            {
                MSystem = MSystem ?? "UCRT64",
                Mirror = Mirror,
                BaseUrl = $"https://github.com/msys2/msys2-installer/releases/download/{msys2Version}/",
                AutoUpdate = true
            };

            // Save the configuration
            await _configuration.SaveConfigurationAsync(config, context.GetCancellationToken());

            console.Out.Write($"\nInitialized MSYS2 environment at {projectRoot}\n");
            console.Out.Write($"  Version: {msys2Version}\n");
            console.Out.Write($"  MSystem: {config.MSystem}\n");
            if (config.Mirror != null)
            {
                console.Out.Write($"  Mirror: {config.Mirror}\n");
            }
            console.Out.Write("\n");
            console.Out.Write("Next steps:\n");
            console.Out.Write("  1. Run 'm2m bootstrap' to install MSYS2\n");
            console.Out.Write("  2. Run 'm2m add <package>' to add packages\n");
            console.Out.Write("  3. Run 'm2m shell' to open a shell\n");

            return 0;
        }
    }
}
