using System.Diagnostics;
using System.IO.Abstractions;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Msys2Manager.Core.Services;

public interface IEnvironmentService
{
    string GetMsys2Root();
    string GetMsysPath();
    string GetTmpPath();
    bool IsMsys2Installed();
    Task<bool> InstallMsys2Async(Uri baseUrl, IProgress<float>? progress = null, CancellationToken cancellationToken = default);
    Task RemoveMsys2Async(bool force = false, CancellationToken cancellationToken = default);
    Task<int> ExecuteCommandAsync(string command, string? workingDirectory = null, CancellationToken cancellationToken = default);
    Task<int> ExecutePacmanAsync(string args, CancellationToken cancellationToken = default);
    Task UpdateAllPackagesAsync(CancellationToken cancellationToken = default);
}

public class EnvironmentService : IEnvironmentService
{
    private readonly IFileSystem _fileSystem;
    private readonly IConfigurationService _configurationService;

    public EnvironmentService(IFileSystem fileSystem, IConfigurationService configurationService)
    {
        _fileSystem = fileSystem;
        _configurationService = configurationService;
    }

    public string GetMsys2Root()
    {
        var projectRoot = _configurationService.GetProjectRoot();
        return _fileSystem.Path.Combine(projectRoot, ".msys2");
    }

    public string GetMsysPath()
    {
        var config = _configurationService.LoadConfigurationAsync().GetAwaiter().GetResult();
        var msysRoot = GetMsys2Root();
        return _fileSystem.Path.Combine(msysRoot, "msys64", config.MSystem switch
        {
            "UCRT64" => "ucrt64",
            "CLANG64" => "clang64",
            "MINGW64" => "mingw64",
            "MINGW32" => "mingw32",
            _ => "usr"
        }).Replace('\\', '/');
    }

    public string GetTmpPath()
    {
        var msysRoot = GetMsys2Root();
        return _fileSystem.Path.Combine(msysRoot, "_tmp");
    }

    public bool IsMsys2Installed()
    {
        var msysRoot = GetMsys2Root();
        var msys64Path = _fileSystem.Path.Combine(msysRoot, "msys64");
        return _fileSystem.Directory.Exists(msys64Path);
    }

    public async Task<bool> InstallMsys2Async(Uri baseUrl, IProgress<float>? progress = null, CancellationToken cancellationToken = default)
    {
        var msysRoot = GetMsys2Root();
        var tmpPath = GetTmpPath();

        _fileSystem.Directory.CreateDirectory(tmpPath);

        // 支持 base_url 两种格式:
        // 1. 完整文件 URL: "https://repo.msys2.org/distrib/x86_64/msys2-base-x86_64-20251213.tar.zst"
        // 2. 目录 URL: "https://repo.msys2.org/distrib/x86_64/20251213/"
        Uri downloadUrl;
        if (baseUrl.AbsolutePath.EndsWith(".tar.zst") || baseUrl.AbsolutePath.EndsWith(".tar.xz"))
        {
            downloadUrl = baseUrl;
        }
        else
        {
            // Extract version from baseUrl path (e.g., "20251213" from ".../distrib/x86_64/20251213/")
            var pathSegments = baseUrl.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var version = pathSegments.Length > 0 ? pathSegments[^1] : "20251213";
            // Prefer .tar.zst as it's smaller (zstd compression)
            downloadUrl = new Uri(baseUrl, $"msys2-base-x86_64-{version}.tar.zst");
        }

        var fileName = System.IO.Path.GetFileName(downloadUrl.LocalPath);
        var downloadPath = _fileSystem.Path.Combine(tmpPath, fileName);

        progress?.Report(0f);

        await DownloadFileAsync(downloadUrl, downloadPath, progress, cancellationToken);

        progress?.Report(0.5f);

        await ExtractArchiveAsync(downloadPath, msysRoot, progress, cancellationToken);

        progress?.Report(1f);

        return true;
    }

    public Task RemoveMsys2Async(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!force)
        {
            Console.Write("Are you sure you want to remove MSYS2? [y/N] ");
            var response = Console.ReadLine();
            if (!response?.Equals("y", StringComparison.OrdinalIgnoreCase) ?? true)
            {
                return Task.CompletedTask;
            }
        }

        var msysRoot = GetMsys2Root();

        if (_fileSystem.Directory.Exists(msysRoot))
        {
            _fileSystem.Directory.Delete(msysRoot, true);
        }

        return Task.CompletedTask;
    }

    public async Task<int> ExecuteCommandAsync(string command, string? workingDirectory = null, CancellationToken cancellationToken = default)
    {
        var msysRoot = GetMsys2Root();
        var msysPath = _fileSystem.Path.Combine(msysRoot, "msys64", "usr", "bin", "bash.exe");
        var config = await _configurationService.LoadConfigurationAsync(cancellationToken);

        workingDirectory ??= _configurationService.GetProjectRoot();

        var msysWorkDir = await ConvertToMsysPathAsync(workingDirectory, cancellationToken);
        var args = $"-l -c \"cd {msysWorkDir} && {command}\"";

        var psi = new ProcessStartInfo
        {
            FileName = msysPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            Environment =
            {
                ["MSYSTEM"] = config.MSystem,
                ["CHERE_INVOKING"] = "1"
            }
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return -1;
        }

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    public async Task<int> ExecutePacmanAsync(string args, CancellationToken cancellationToken = default)
    {
        return await ExecuteCommandAsync($"pacman --noconfirm {args}", cancellationToken: cancellationToken);
    }

    public async Task UpdateAllPackagesAsync(CancellationToken cancellationToken = default)
    {
        await ExecutePacmanAsync("-Syu", cancellationToken);
        await ExecutePacmanAsync("-Su", cancellationToken);
    }

    private async Task<string> ConvertToMsysPathAsync(string windowsPath, CancellationToken cancellationToken)
    {
        var msysRoot = GetMsys2Root();
        var msysBash = _fileSystem.Path.Combine(msysRoot, "msys64", "usr", "bin", "bash.exe");

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = msysBash,
            Arguments = $"-l -c \"cygpath -u '{windowsPath}'\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        });

        if (process is null)
            return windowsPath;

        await process.WaitForExitAsync(cancellationToken);
        var msysPath = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        return msysPath.Trim();
    }

    private async Task DownloadFileAsync(Uri uri, string destination, IProgress<float>? progress, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        var buffer = new byte[81920];

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = _fileSystem.File.Create(destination);

        var totalRead = 0L;
        var read = 0;

        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
            totalRead += read;

            if (totalBytes > 0 && progress is not null)
            {
                var percent = (float)totalRead / totalBytes * 0.5f; // Download is 50% of total
                progress.Report(percent);
            }
        }
    }

    private async Task ExtractArchiveAsync(string archivePath, string destination, IProgress<float>? progress, CancellationToken cancellationToken)
    {
        // SharpCompress supports .tar.zst (zstd), .tar.xz, .tar.gz, etc. directly
        await Task.Run(() =>
        {
            _fileSystem.Directory.CreateDirectory(destination);

            using var archive = ArchiveFactory.Open(archivePath);
            var entries = archive.Entries.Where(x => !x.IsDirectory).ToArray();
            var totalEntries = entries.Length;
            var extracted = 0;

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                entry.WriteToDirectory(destination, new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true
                });

                extracted++;

                // Report progress: 0.5 to 1.0 (50% to 100%)
                if (progress is not null && totalEntries > 0)
                {
                    var percent = 0.5f + (float)extracted / totalEntries * 0.5f;
                    progress.Report(percent);
                }
            }
        }, cancellationToken);

        // Clean up extracted intermediate file if exists
        var tarPath = archivePath + ".tar";
        if (_fileSystem.File.Exists(tarPath))
        {
            _fileSystem.File.Delete(tarPath);
        }
    }
}
