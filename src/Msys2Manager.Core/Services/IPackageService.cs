using System.Diagnostics;
using Msys2Manager.Core.Configuration;

namespace Msys2Manager.Core.Services;

public interface IPackageService
{
    Task<IReadOnlyList<string>> GetInstalledPackagesAsync(CancellationToken cancellationToken = default);
    Task<bool> InstallPackageAsync(string packageName, string? versionConstraint = null, CancellationToken cancellationToken = default);
    Task<bool> UninstallPackageAsync(string packageName, CancellationToken cancellationToken = default);
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

        var msysRoot = _environmentService.GetMsys2Root();
        var pacman = System.IO.Path.Combine(msysRoot, "msys64", "usr", "bin", "pacman.exe");

        var startInfo = new ProcessStartInfo
        {
            FileName = pacman,
            Arguments = "-Q --explicit",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
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

    public async Task<bool> InstallPackageAsync(string packageName, string? versionConstraint = null, CancellationToken cancellationToken = default)
    {
        var version = versionConstraint ?? "*";
        var pacmanArgs = $"-S --needed {packageName}";

        if (version != "*")
        {
            pacmanArgs += $"={version}";
        }

        var msysRoot = _environmentService.GetMsys2Root();
        var pacman = System.IO.Path.Combine(msysRoot, "msys64", "usr", "bin", "pacman.exe");
        var config = await _configurationService.LoadConfigurationAsync(cancellationToken);

        // Set environment variables for this process (will be inherited by pacman)
        System.Environment.SetEnvironmentVariable("MSYSTEM", config.MSystem);

        // Install with direct output (user sees progress)
        var psi = new ProcessStartInfo
        {
            FileName = pacman,
            Arguments = $"--noconfirm {pacmanArgs}",
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        if (process is null)
            return false;

        process.WaitForExit();
        var exitCode = process.ExitCode;

        if (exitCode != 0)
            return false;

        // Verify installation by querying package (capture output, check exit code)
        psi = new ProcessStartInfo
        {
            FileName = pacman,
            Arguments = $"-Q {packageName}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            CreateNoWindow = true
        };

        using var verifyProcess = Process.Start(psi);
        if (verifyProcess is null)
            return false;

        verifyProcess.WaitForExit();
        return verifyProcess.ExitCode == 0;
    }

    public async Task<bool> UninstallPackageAsync(string packageName, CancellationToken cancellationToken = default)
    {
        var msysRoot = _environmentService.GetMsys2Root();
        var pacman = System.IO.Path.Combine(msysRoot, "msys64", "usr", "bin", "pacman.exe");
        var config = await _configurationService.LoadConfigurationAsync(cancellationToken);

        // Set environment variables for this process (will be inherited by pacman)
        System.Environment.SetEnvironmentVariable("MSYSTEM", config.MSystem);

        Console.WriteLine($"Removing {packageName}...");

        // Remove with direct output (user sees progress)
        var psi = new ProcessStartInfo
        {
            FileName = pacman,
            Arguments = $"--noconfirm -R {packageName}",
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        if (process is null)
            return false;

        process.WaitForExit();
        var exitCode = process.ExitCode;

        if (exitCode != 0)
        {
            Console.WriteLine($"Failed to remove {packageName} (exit code: {exitCode})");
            return false;
        }

        // Verify package is removed
        psi = new ProcessStartInfo
        {
            FileName = pacman,
            Arguments = $"-Q {packageName}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            CreateNoWindow = true
        };

        using var verifyProcess = Process.Start(psi);
        if (verifyProcess is null)
            return true; // If we can't verify, assume success

        verifyProcess.WaitForExit();
        var verified = verifyProcess.ExitCode != 0; // Q should fail if package is removed
        Console.WriteLine($"Verified removal of {packageName}: {verified}");
        return verified;
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
            var success = await InstallPackageAsync(package, version, cancellationToken);
            if (!success)
            {
                Console.WriteLine($"Failed to install {package}. Aborting.");
                return;
            }
        }

        if (prune)
        {
            foreach (var package in toRemove)
            {
                Console.WriteLine($"Removing {package}...");
                var success = await UninstallPackageAsync(package, cancellationToken);
                if (!success)
                {
                    Console.WriteLine($"Failed to remove {package}. Aborting.");
                    return;
                }
            }
        }
    }
}
