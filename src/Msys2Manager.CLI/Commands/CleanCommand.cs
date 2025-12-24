using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class CleanCommand : Command
{
    public CleanCommand() : base("clean", "Remove MSYS2 environment")
    {
        var forceOption = new Option<bool>(
            ["--force", "-f"],
            "Skip confirmation"
        );

        AddOption(forceOption);
    }

    public new class Handler : ICommandHandler
    {
        private readonly IEnvironmentService _environment;

        public CleanCommand(IEnvironmentService environment)
        {
            _environment = environment;
        }

        public bool Force { get; set; }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var console = context.Console;

            if (!_environment.IsMsys2Installed())
            {
                console.Out.WriteLine("MSYS2 is not installed.");
                return 0;
            }

            try
            {
                await _environment.RemoveMsys2Async(Force, context.GetCancellationToken());
                console.Out.WriteLine("MSYS2 environment removed.");
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
