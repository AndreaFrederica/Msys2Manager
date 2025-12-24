using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class RemoveCommand : Command
{
    public RemoveCommand() : base("remove", "Remove a package from configuration")
    {
        AddArgument(new Argument<string[]>("packages", "Package names to remove"));
    }

    public new class Handler : ICommandHandler
    {
        private readonly IPackageService _packages;

        public RemoveCommand(IPackageService packages)
        {
            _packages = packages;
        }

        public string[] Packages { get; set; } = Array.Empty<string>();

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var console = context.Console;

            try
            {
                foreach (var package in Packages)
                {
                    await _packages.RemovePackageFromConfigAsync(package, context.GetCancellationToken());
                }

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
