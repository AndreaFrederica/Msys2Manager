using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.CLI.Commands;

namespace Msys2Manager.CLI;

public static class DependencyInjection
{
    public static IServiceCollection AddCliServices(this IServiceCollection services)
    {
        services.AddTransient<BootstrapCommand.Handler>();
        services.AddTransient<UpdateCommand.Handler>();
        services.AddTransient<SyncCommand.Handler>();
        services.AddTransient<AddCommand.Handler>();
        services.AddTransient<RemoveCommand.Handler>();
        services.AddTransient<RunCommand.Handler>();
        services.AddTransient<ShellCommand.Handler>();
        services.AddTransient<CleanCommand.Handler>();

        return services;
    }
}
