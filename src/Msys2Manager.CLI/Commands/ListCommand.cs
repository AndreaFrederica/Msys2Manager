using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class ListCommand : Command
{
    public readonly Option<bool> ExplicitOption;
    public readonly Option<bool> LockFileOption;

    public ListCommand() : base("list", "List installed packages")
    {
        ExplicitOption = new Option<bool>(
            ["--explicit", "-e"],
            "Show only explicitly installed packages"
        );

        LockFileOption = new Option<bool>(
            ["--lock-file", "-l"],
            "Show packages from lock file"
        );

        AddOption(ExplicitOption);
        AddOption(LockFileOption);
    }

    public new class Handler : ICommandHandler
    {
        private readonly IPackageService _packages;
        private readonly IConfigurationService _configuration;

        public Handler(IPackageService packages, IConfigurationService configuration)
        {
            _packages = packages;
            _configuration = configuration;
        }

        public bool Explicit { get; set; }
        public bool LockFile { get; set; }

        public int Invoke(InvocationContext context)
        {
            return InvokeAsync(context).GetAwaiter().GetResult();
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var console = context.Console;

            if (LockFile)
            {
                return await ListFromLockFileAsync(console, context.GetCancellationToken());
            }

            return await ListInstalledAsync(console, context.GetCancellationToken());
        }

        private async Task<int> ListInstalledAsync(IConsole console, CancellationToken cancellationToken)
        {
            try
            {
                var installed = await _packages.GetInstalledPackagesAsync(cancellationToken);

                if (installed.Count == 0)
                {
                    console.Out.Write("No packages installed.\n");
                    return 0;
                }

                console.Out.Write("Installed packages:\n\n");
                foreach (var package in installed)
                {
                    console.Out.Write($"  {package}\n");
                }

                console.Out.Write($"\nTotal: {installed.Count} package(s).\n");
                return 0;
            }
            catch (Exception ex)
            {
                console.Error.Write($"Error: {ex.Message}\n");
                return 1;
            }
        }

        private async Task<int> ListFromLockFileAsync(IConsole console, CancellationToken cancellationToken)
        {
            try
            {
                var lockFile = await _configuration.LoadLockFileAsync(cancellationToken);

                if (lockFile.Packages.Count == 0)
                {
                    console.Out.Write("Lock file is empty or does not exist.\n");
                    return 0;
                }

                console.Out.Write("Packages from lock file:\n\n");
                foreach (var package in lockFile.Packages)
                {
                    console.Out.Write($"  {package}\n");
                }

                console.Out.Write($"\nTotal: {lockFile.Packages.Count} package(s).\n");
                return 0;
            }
            catch (Exception ex)
            {
                console.Error.Write($"Error: {ex.Message}\n");
                return 1;
            }
        }
    }
}
