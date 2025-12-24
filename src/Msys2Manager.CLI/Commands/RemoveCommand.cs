using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class RemoveCommand : Command
{
    public readonly Argument<string[]> PackagesArgument;

    public RemoveCommand() : base("remove", "Remove a package from configuration")
    {
        PackagesArgument = new Argument<string[]>("packages", "Package names to remove");
        AddArgument(PackagesArgument);
    }

    public new class Handler : ICommandHandler
    {
        private readonly IPackageService _packages;

        public Handler(IPackageService packages)
        {
            _packages = packages;
        }

        public string[] Packages { get; set; } = Array.Empty<string>();

        public int Invoke(InvocationContext context)
        {
            return InvokeAsync(context).GetAwaiter().GetResult();
        }

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
                console.Error.Write($"Error: {ex.Message}"
);
                return 1;
            }
        }
    }
}
