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

        var fileName = "msys2-base-x86_64.tar.zst";
        var downloadPath = _fileSystem.Path.Combine(tmpPath, fileName);

        progress?.Report(0f);

        await DownloadFileAsync(new Uri(baseUrl, fileName), downloadPath, progress, cancellationToken);

        progress?.Report(0.5f);

        await ExtractArchiveAsync(downloadPath, msysRoot, progress, cancellationToken);

        progress?.Report(1f);

        return true;
    }

    public async Task RemoveMsys2Async(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!force)
        {
            Console.Write("Are you sure you want to remove MSYS2? [y/N] ");
            var response = Console.ReadLine();
            if (!response?.Equals("y", StringComparison.OrdinalIgnoreCase) ?? true)
            {
                return;
            }
        }

        var msysRoot = GetMsys2Root();

        if (_fileSystem.Directory.Exists(msysRoot))
        {
            _fileSystem.Directory.Delete(msysRoot, true);
        }
    }

    public async Task<int> ExecuteCommandAsync(string command, string? workingDirectory = null, CancellationToken cancellationToken = default)
    {
        var msysRoot = GetMsys2Root();
        var msysPath = _fileSystem.Path.Combine(msysRoot, "msys64", "usr", "bin", "bash.exe");
        var config = await _configurationService.LoadConfigurationAsync(cancellationToken);

        workingDirectory ??= _configurationService.GetProjectRoot();

        var args = $"-l -c \"cd {ConvertToMsysPath(workingDirectory)} && {command}\"";

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

    private string ConvertToMsysPath(string windowsPath)
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
        await using var fileStream = _fileSystem.FileStream.Create(destination, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true);

        var totalRead = 0L;
        var read = 0;

        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
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
        var zstdPath = await FindOrDownloadZstdAsync(cancellationToken);

        var startInfo = new ProcessStartInfo
        {
            FileName = zstdPath,
            Arguments = $"-d \"{archivePath}\" -fo \"{archivePath}.tar\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var zstd = Process.Start(startInfo);
        await zstd!.WaitForExitAsync(cancellationToken);

        var tarArgs = $"-xf \"{archivePath}.tar\" -C \"{destination}\"";

        startInfo = new ProcessStartInfo
        {
            FileName = "tar",
            Arguments = tarArgs,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var tar = Process.Start(startInfo);
        await tar!.WaitForExitAsync(cancellationToken);
    }

    private async Task<string> FindOrDownloadZstdAsync(CancellationToken cancellationToken)
    {
        var systemPaths = new[]
        {
            @"C:\Program Files\7-Zip\zstd.exe",
            @"C:\Program Files (x86)\7-Zip\zstd.exe"
        };

        foreach (var path in systemPaths)
        {
            if (_fileSystem.File.Exists(path))
                return path;
        }

        var tmpPath = GetTmpPath();
        var zstdPath = _fileSystem.Path.Combine(tmpPath, "zstd.exe");

        if (!_fileSystem.File.Exists(zstdPath))
        {
            await DownloadFileAsync(new Uri("https://github.com/facebook/zstd/releases/download/v1.5.6/zstd-v1.5.6-win64.zip"), zstdPath + ".zip", null, cancellationToken);
            await ExtractArchiveAsync(zstdPath + ".zip", tmpPath, null, cancellationToken);
        }

        return zstdPath;
    }
}
