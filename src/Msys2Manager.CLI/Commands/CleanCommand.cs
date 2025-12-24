using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class CleanCommand : Command
{
    public readonly Option<bool> ForceOption;

    public CleanCommand() : base("clean", "Remove MSYS2 environment")
    {
        ForceOption = new Option<bool>(
            ["--force", "-f"],
            "Skip confirmation"
        );

        AddOption(ForceOption);
    }

    public new class Handler : ICommandHandler
    {
        private readonly IEnvironmentService _environment;

        public Handler(IEnvironmentService environment)
        {
            _environment = environment;
        }

        public bool Force { get; set; }

        public int Invoke(InvocationContext context)
        {
            return InvokeAsync(context).GetAwaiter().GetResult();
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var console = context.Console;

            if (!_environment.IsMsys2Installed())
            {
                console.Out.Write("MSYS2 is not installed."
);
                return 0;
            }

            try
            {
                await _environment.RemoveMsys2Async(Force, context.GetCancellationToken());
                console.Out.Write("MSYS2 environment removed."
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
