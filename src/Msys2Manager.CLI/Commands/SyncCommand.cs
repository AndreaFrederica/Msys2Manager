using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class SyncCommand : Command
{
    public SyncCommand() : base("sync", "Sync installed packages with configuration")
    {
        var pruneOption = new Option<bool>(
            ["--prune", "-p"],
            "Remove packages not in configuration"
        );

        AddOption(pruneOption);
    }

    public new class Handler : ICommandHandler
    {
        private readonly IPackageService _packages;
        private readonly IEnvironmentService _environment;

        public Handler(IPackageService packages, IEnvironmentService environment)
        {
            _packages = packages;
            _environment = environment;
        }

        public bool Prune { get; set; }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var console = context.Console;

            if (!_environment.IsMsys2Installed())
            {
                console.Error.WriteLine("MSYS2 is not installed. Run 'm2m bootstrap' first.");
                return 1;
            }

            console.Out.WriteLine("Syncing packages...");

            try
            {
                await _packages.SyncPackagesAsync(Prune, context.GetCancellationToken());
                console.Out.WriteLine("Sync complete.");
                return 0;
            }
            catch (Exception ex)
            {
                console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }
    }
}
