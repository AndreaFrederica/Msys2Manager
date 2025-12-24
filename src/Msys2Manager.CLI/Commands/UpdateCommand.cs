using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class UpdateCommand : Command
{
    public UpdateCommand() : base("update", "Update all MSYS2 packages")
    {
    }

    public new class Handler : ICommandHandler
    {
        private readonly IEnvironmentService _environment;
        private readonly IConfigurationService _configuration;

        public Handler(IEnvironmentService environment, IConfigurationService configuration)
        {
            _environment = environment;
            _configuration = configuration;
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var console = context.Console;

            if (!_environment.IsMsys2Installed())
            {
                console.Error.WriteLine("MSYS2 is not installed. Run 'm2m bootstrap' first.");
                return 1;
            }

            console.Out.WriteLine("Updating MSYS2 packages...");

            try
            {
                await _environment.UpdateAllPackagesAsync(context.GetCancellationToken());

                var lockFile = await _configuration.LoadLockFileAsync(context.GetCancellationToken());
                await _configuration.SaveLockFileAsync(lockFile, context.GetCancellationToken());

                console.Out.WriteLine("Update complete.");
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
