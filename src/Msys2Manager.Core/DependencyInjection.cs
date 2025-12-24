using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.IO.Abstractions;

namespace Msys2Manager.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IEnvironmentService, EnvironmentService>();
        services.AddSingleton<IPackageService, PackageService>();
        services.AddSingleton<ITaskService, TaskService>();

        return services;
    }
}
