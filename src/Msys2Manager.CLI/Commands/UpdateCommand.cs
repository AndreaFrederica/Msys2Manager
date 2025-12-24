using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class UpdateCommand : Command
{
    public UpdateCommand() : base("update", "Update package database (pacman -Sy)")
    {
    }

    public new class Handler : ICommandHandler
    {
        private readonly IEnvironmentService _environment;

        public Handler(IEnvironmentService environment)
        {
            _environment = environment;
        }

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

            console.Out.Write("Updating package database...\n");

            try
            {
                await _environment.UpdatePackageListAsync(context.GetCancellationToken());

                console.Out.Write("Package database updated.\n");
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
