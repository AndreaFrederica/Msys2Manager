using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class AddCommand : Command
{
    public AddCommand() : base("add", "Add a package to configuration and install it")
    {
        var versionOption = new Option<string?>(
            ["--version", "-v"],
            "Version constraint (e.g., '6.6.*', '1.0.0')"
        );

        AddArgument(new Argument<string[]>("packages", "Package names to add"));
        AddOption(versionOption);
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

        public string[] Packages { get; set; } = Array.Empty<string>();
        public string? Version { get; set; }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var console = context.Console;

            if (!_environment.IsMsys2Installed())
            {
                console.Error.WriteLine("MSYS2 is not installed. Run 'm2m bootstrap' first.");
                return 1;
            }

            try
            {
                foreach (var package in Packages)
                {
                    await _packages.AddPackageToConfigAsync(package, Version, context.GetCancellationToken());
                    await _packages.InstallPackageAsync(package, Version, context.GetCancellationToken());
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
