using System.Diagnostics;
using Msys2Manager.Core.Configuration;

namespace Msys2Manager.Core.Services;

public interface IPackageService
{
    Task<IReadOnlyList<string>> GetInstalledPackagesAsync(CancellationToken cancellationToken = default);
    Task InstallPackageAsync(string packageName, string? versionConstraint = null, CancellationToken cancellationToken = default);
    Task UninstallPackageAsync(string packageName, CancellationToken cancellationToken = default);
    Task AddPackageToConfigAsync(string packageName, string? versionConstraint = null, CancellationToken cancellationToken = default);
    Task RemovePackageFromConfigAsync(string packageName, CancellationToken cancellationToken = default);
    Task SyncPackagesAsync(bool prune = false, CancellationToken cancellationToken = default);
}

public class PackageService : IPackageService
{
    private readonly IEnvironmentService _environmentService;
    private readonly IConfigurationService _configurationService;

    public PackageService(IEnvironmentService environmentService, IConfigurationService configurationService)
    {
        _environmentService = environmentService;
        _configurationService = configurationService;
    }

    public async Task<IReadOnlyList<string>> GetInstalledPackagesAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<string>();

        var startInfo = new ProcessStartInfo
        {
            FileName = _environmentService.GetMsysPath() + "/pacman.exe",
            Arguments = "-Q",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
            return result;

        await process.WaitForExitAsync(cancellationToken);

        while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
        {
            var parts = line.Split(' ');
            if (parts.Length >= 2)
            {
                result.Add($"{parts[0]} {parts[1]}");
            }
        }

        return result;
    }

    public async Task InstallPackageAsync(string packageName, string? versionConstraint = null, CancellationToken cancellationToken = default)
    {
        var version = versionConstraint ?? "*";
        var args = $"-S --needed {packageName}";

        if (version != "*")
        {
            args += $"={version}";
        }

        await _environmentService.ExecutePacmanAsync(args, cancellationToken);
    }

    public async Task UninstallPackageAsync(string packageName, CancellationToken cancellationToken = default)
    {
        await _environmentService.ExecutePacmanAsync($"-R {packageName}", cancellationToken);
    }

    public async Task AddPackageToConfigAsync(string packageName, string? versionConstraint = null, CancellationToken cancellationToken = default)
    {
        var config = await _configurationService.LoadConfigurationAsync(cancellationToken);

        if (config.Packages.ContainsKey(packageName))
        {
            Console.WriteLine($"Package {packageName} is already in configuration.");
            return;
        }

        config.Packages.Add(packageName, versionConstraint ?? "*");
        await _configurationService.SaveConfigurationAsync(config, cancellationToken);

        Console.WriteLine($"Added {packageName}={versionConstraint ?? "*"} to msys2.toml");
    }

    public async Task RemovePackageFromConfigAsync(string packageName, CancellationToken cancellationToken = default)
    {
        var config = await _configurationService.LoadConfigurationAsync(cancellationToken);

        if (!config.Packages.ContainsKey(packageName))
        {
            Console.WriteLine($"Package {packageName} is not in configuration.");
            return;
        }

        config.Packages.Remove(packageName);
        await _configurationService.SaveConfigurationAsync(config, cancellationToken);

        Console.WriteLine($"Removed {packageName} from msys2.toml");
    }

    public async Task SyncPackagesAsync(bool prune = false, CancellationToken cancellationToken = default)
    {
        var config = await _configurationService.LoadConfigurationAsync(cancellationToken);
        var lockFile = await _configurationService.LoadLockFileAsync(cancellationToken);

        var installedPackages = await GetInstalledPackagesAsync(cancellationToken);
        var installedNames = installedPackages
            .Select(p => p.Split(' ')[0])
            .ToHashSet();

        var configPackages = config.Packages.Keys.ToHashSet();

        var toInstall = configPackages.Except(installedNames).ToList();
        var toRemove = prune ? installedNames.Except(configPackages).ToList() : new List<string>();

        foreach (var package in toInstall)
        {
            var version = config.Packages[package];
            Console.WriteLine($"Installing {package}={version}...");
            await InstallPackageAsync(package, version, cancellationToken);
        }

        if (prune)
        {
            foreach (var package in toRemove)
            {
                Console.WriteLine($"Removing {package}...");
                await UninstallPackageAsync(package, cancellationToken);
            }
        }
    }
}
