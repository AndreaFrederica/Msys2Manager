using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class BootstrapCommand : Command
{
    public BootstrapCommand() : base("bootstrap", "Initialize MSYS2 environment")
    {
        var urlOption = new Option<string>(
            ["--url", "-u"],
            () => "https://github.com/msys2/msys2-installer/releases/download/2024-01-13/",
            "Base URL for MSYS2 installation"
        );

        AddOption(urlOption);
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

        public string Url { get; set; } = "https://github.com/msys2/msys2-installer/releases/download/2024-01-13/";

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var console = context.Console;

            if (_environment.IsMsys2Installed())
            {
                console.Out.WriteLine("MSYS2 is already installed.");
                return 0;
            }

            console.Out.WriteLine("Installing MSYS2...");

            var progress = new Progress<float>(p =>
            {
                console.Out.Write($"\rProgress: {p * 100:F1}%");
            });

            try
            {
                await _environment.InstallMsys2Async(new Uri(Url), progress, context.GetCancellationToken());
                console.Out.WriteLine("\nMSYS2 installed successfully.");

                var config = await _configuration.LoadConfigurationAsync(context.GetCancellationToken());

                if (config.AutoUpdate)
                {
                    console.Out.WriteLine("Updating packages...");
                    await _environment.UpdateAllPackagesAsync(context.GetCancellationToken());
                }

                return 0;
            }
            catch (Exception ex)
            {
                console.Error.WriteLine($"\nError: {ex.Message}");
                return 1;
            }
        }
    }
}
