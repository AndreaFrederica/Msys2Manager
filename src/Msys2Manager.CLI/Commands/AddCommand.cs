using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class AddCommand : Command
{
    public readonly Option<string?> VersionOption;
    public readonly Argument<string[]> PackagesArgument;

    public AddCommand() : base("add", "Add a package to configuration and install it")
    {
        VersionOption = new Option<string?>(
            ["--version", "-v"],
            "Version constraint (e.g., '6.6.*', '1.0.0')"
        );

        PackagesArgument = new Argument<string[]>("packages", "Package names to add");
        AddArgument(PackagesArgument);
        AddOption(VersionOption);
    }

    public new class Handler : ICommandHandler
    {
        private readonly IPackageService _packages;
        private readonly IEnvironmentService _environment;
        private readonly IConfigurationService _configuration;

        public Handler(IPackageService packages, IEnvironmentService environment, IConfigurationService configuration)
        {
            _packages = packages;
            _environment = environment;
            _configuration = configuration;
        }

        public string[] Packages { get; set; } = Array.Empty<string>();
        public string? Version { get; set; }

        public int Invoke(InvocationContext context)
        {
            return InvokeAsync(context).GetAwaiter().GetResult();
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var console = context.Console;

            if (!_environment.IsMsys2Installed())
            {
                console.Error.Write("MSYS2 is not installed. Run 'm2m bootstrap' first.\n");
                return 1;
            }

            try
            {
                foreach (var package in Packages)
                {
                    // Install first, verify success before adding to config
                    var success = await _packages.InstallPackageAsync(package, Version, context.GetCancellationToken());
                    if (!success)
                    {
                        console.Error.Write($"Failed to install {package}. Not adding to configuration.\n");
                        return 1;
                    }

                    // Get the actual installed version
                    var installedVersion = await GetInstalledPackageVersionAsync(package, context.GetCancellationToken());
                    if (installedVersion is null)
                    {
                        console.Error.Write($"Failed to get installed version for {package}.\n");
                        return 1;
                    }

                    await _packages.AddPackageToConfigAsync(package, installedVersion, context.GetCancellationToken());
                }

                // Generate lock file after adding packages
                await GenerateLockFileAsync(context.GetCancellationToken());
                console.Out.Write("Generating msys2.lock...\n");

                return 0;
            }
            catch (Exception ex)
            {
                console.Error.Write($"Error: {ex.Message}\n");
                return 1;
            }
        }

        private async Task<string?> GetInstalledPackageVersionAsync(string packageName, CancellationToken cancellationToken)
        {
            var msysRoot = _environment.GetMsys2Root();
            var pacman = System.IO.Path.Combine(msysRoot, "msys64", "usr", "bin", "pacman.exe");
            var config = await _configuration.LoadConfigurationAsync(cancellationToken);

            System.Environment.SetEnvironmentVariable("MSYSTEM", config.MSystem);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pacman,
                Arguments = $"-Q {packageName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null)
                return null;

            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
                return null;

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var parts = output.Trim().Split(' ');
            return parts.Length >= 2 ? parts[1] : null;
        }

        private async Task GenerateLockFileAsync(CancellationToken cancellationToken)
        {
            var msysRoot = _environment.GetMsys2Root();
            var pacman = System.IO.Path.Combine(msysRoot, "msys64", "usr", "bin", "pacman.exe");
            var config = await _configuration.LoadConfigurationAsync(cancellationToken);
            var projectRoot = _configuration.GetProjectRoot();
            var lockPath = System.IO.Path.Combine(projectRoot, "msys2.lock");

            // Set environment variables
            System.Environment.SetEnvironmentVariable("MSYSTEM", config.MSystem);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pacman,
                Arguments = "-Q --explicit",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null)
                return;

            var lines = new List<string>();
            while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
            {
                // Convert "package version" to "package=version"
                var parts = line.Split(' ');
                if (parts.Length >= 2)
                {
                    lines.Add($"{parts[0]}={parts[1]}");
                }
            }

            await process.WaitForExitAsync(cancellationToken);
            await System.IO.File.WriteAllLinesAsync(lockPath, lines, cancellationToken);
        }
    }
}
