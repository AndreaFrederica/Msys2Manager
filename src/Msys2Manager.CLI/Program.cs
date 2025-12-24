using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.CLI.Commands;
using Msys2Manager.Core;
using System.CommandLine;

namespace Msys2Manager.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddCoreServices();
        services.AddCliServices();

        var serviceProvider = services.BuildServiceProvider();

        var rootCommand = new RootCommand("M2M - MSYS2 Management Tool")
        {
            Description = "Manage MSYS2 environments and packages with ease"
        };

        rootCommand.AddCommands(serviceProvider);

        return await rootCommand.InvokeAsync(args);
    }
}
