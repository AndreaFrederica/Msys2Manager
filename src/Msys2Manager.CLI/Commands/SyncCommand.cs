using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class SyncCommand : Command
{
    public readonly Option<bool> PruneOption;

    public SyncCommand() : base("sync", "Sync installed packages with configuration")
    {
        PruneOption = new Option<bool>(
            ["--prune", "-p"],
            "Remove packages not in configuration"
        );

        AddOption(PruneOption);
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

        public int Invoke(InvocationContext context)
        {
            return InvokeAsync(context).GetAwaiter().GetResult();
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var console = context.Console;

            if (!_environment.IsMsys2Installed())
            {
                console.Error.Write("MSYS2 is not installed. Run 'm2m bootstrap' first."
);
                return 1;
            }

            console.Out.Write("Syncing packages..."
);

            try
            {
                await _packages.SyncPackagesAsync(Prune, context.GetCancellationToken());
                console.Out.Write("Sync complete."
);
                return 0;
            }
            catch (Exception ex)
            {
                console.Error.Write($"Error: {ex.Message}"
);
                return 1;
            }
        }
    }
}
