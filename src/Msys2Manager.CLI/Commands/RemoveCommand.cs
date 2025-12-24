using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class RemoveCommand : Command
{
    public readonly Argument<string[]> PackagesArgument;

    public RemoveCommand() : base("remove", "Remove packages from configuration and uninstall them")
    {
        PackagesArgument = new Argument<string[]>("packages", "Package names to remove");
        AddArgument(PackagesArgument);
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
                // Uninstall first, verify success before removing from config
                console.Out.Write("Uninstalling packages...\n");
                foreach (var package in Packages)
                {
                    var success = await _packages.UninstallPackageAsync(package, context.GetCancellationToken());
                    if (!success)
                    {
                        console.Error.Write($"Failed to uninstall {package}. Not removing from configuration.\n");
                        return 1;
                    }
                }

                // Successfully uninstalled, now remove from config
                foreach (var package in Packages)
                {
                    await _packages.RemovePackageFromConfigAsync(package, context.GetCancellationToken());
                }

                // Generate lock file after removing packages
                await GenerateLockFileAsync(context.GetCancellationToken());
                console.Out.Write("Generating msys2.lock...\n");
                console.Out.Write("Packages removed successfully.\n");

                return 0;
            }
            catch (Exception ex)
            {
                console.Error.Write($"Error: {ex.Message}\n");
                return 1;
            }
        }

        private async Task GenerateLockFileAsync(CancellationToken cancellationToken)
        {
            var msysRoot = _environment.GetMsys2Root();
            var pacman = System.IO.Path.Combine(msysRoot, "msys64", "usr", "bin", "pacman.exe");
            var config = await _configuration.LoadConfigurationAsync(cancellationToken);
            var projectRoot = _configuration.GetProjectRoot();
            var lockPath = System.IO.Path.Combine(projectRoot, "msys2.lock");

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
