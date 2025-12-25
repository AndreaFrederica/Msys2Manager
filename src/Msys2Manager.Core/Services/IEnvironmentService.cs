using System.Diagnostics;
using System.IO.Abstractions;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Msys2Manager.Core.Services;

/// <summary>
/// Download progress information
/// </summary>
public class DownloadProgress
{
    public float Percent { get; set; }
    public long DownloadedBytes { get; set; }
    public long TotalBytes { get; set; }
    public double SpeedBytesPerSecond { get; set; }
    public TimeSpan? Eta { get; set; }
    public bool IsCached { get; set; }

    public string FormattedSpeed => IsCached ? "Cached" : FormatBytes(SpeedBytesPerSecond) + "/s";
    public string FormattedEta => Eta.HasValue ? FormatTime(Eta.Value) : "--:--";
    public string FormattedProgress => $"{FormatBytes(DownloadedBytes)} / {FormatBytes(TotalBytes)}";
    public string FormattedPercent => $"{Percent * 100:F1}%";

    /// <summary>
    /// Get a visual progress bar (e.g., [████████░░░░░░░░░] 40%)
    /// </summary>
    /// <param name="width">Total width of the progress bar in characters (default: 30)</param>
    /// <returns>Visual progress bar string</returns>
    public string GetProgressBar(int width = 30)
    {
        var filled = (int)Math.Round(Percent * width);
        var empty = width - filled;
        return $"[{new string('█', filled)}{new string('░', empty)}]";
    }

    /// <summary>
    /// Get a complete progress display string
    /// </summary>
    /// <param name="barWidth">Width of the progress bar (default: 20)</param>
    /// <returns>Complete progress display string</returns>
    public string GetProgressDisplay(int barWidth = 20)
    {
        return $"{GetProgressBar(barWidth)} {FormattedPercent} | {FormattedProgress} | {FormattedSpeed} | ETA: {FormattedEta}";
    }

    private static string FormatBytes(double bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    private static string FormatTime(TimeSpan ts)
    {
        if (ts.TotalSeconds < 60)
            return $"{(int)ts.TotalSeconds}s";
        if (ts.TotalMinutes < 60)
            return $"{(int)ts.TotalMinutes}m {(int)ts.Seconds}s";
        return $"{(int)ts.TotalHours}h {(int)ts.Minutes}m";
    }
}

public interface IEnvironmentService
{
    string GetMsys2Root();
    string GetMsysPath();
    string GetTmpPath();
    bool IsMsys2Installed();
    Task<bool> InstallMsys2Async(Uri baseUrl, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);
    Task RemoveMsys2Async(bool force = false, CancellationToken cancellationToken = default);
    Task<int> ExecuteCommandAsync(string command, string? workingDirectory = null, bool redirectOutput = false, CancellationToken cancellationToken = default);
    Task<int> ExecutePacmanAsync(string args, bool redirectOutput = false, CancellationToken cancellationToken = default);
    Task UpdatePackageListAsync(CancellationToken cancellationToken = default);
    Task UpgradeInstalledPackagesAsync(CancellationToken cancellationToken = default);
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

    public async Task<bool> InstallMsys2Async(Uri baseUrl, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var msysRoot = GetMsys2Root();
        var tmpPath = GetTmpPath();

        _fileSystem.Directory.CreateDirectory(tmpPath);

        // 支持 base_url 两种格式:
        // 1. 完整文件 URL: "https://repo.msys2.org/distrib/x86_64/msys2-base-x86_64-20251213.tar.xz"
        // 2. 目录 URL: "https://repo.msys2.org/distrib/x86_64/"
        Uri downloadUrl;
        if (baseUrl.AbsolutePath.EndsWith(".tar.zst") || baseUrl.AbsolutePath.EndsWith(".tar.xz"))
        {
            downloadUrl = baseUrl;
        }
        else
        {
            // Get version from config to construct filename
            var config = await _configurationService.LoadConfigurationAsync(cancellationToken);
            var versionForUrl = config.Version?.Replace("-", "") ?? "20251213";
            // Use .tar.xz since SharpCompress doesn't support zstd compression
            downloadUrl = new Uri(baseUrl, $"msys2-base-x86_64-{versionForUrl}.tar.xz");
        }

        var fileName = System.IO.Path.GetFileName(downloadUrl.LocalPath);
        var downloadPath = _fileSystem.Path.Combine(tmpPath, fileName);

        progress?.Report(new DownloadProgress { Percent = 0f });

        await DownloadFileAsync(downloadUrl, downloadPath, progress, cancellationToken);

        progress?.Report(new DownloadProgress { Percent = 0.5f });

        await ExtractArchiveAsync(downloadPath, msysRoot, progress, cancellationToken);

        progress?.Report(new DownloadProgress { Percent = 1f });

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

    public async Task<int> ExecuteCommandAsync(string command, string? workingDirectory = null, bool redirectOutput = false, CancellationToken cancellationToken = default)
    {
        var msysRoot = GetMsys2Root();
        var msysPath = _fileSystem.Path.Combine(msysRoot, "msys64", "usr", "bin", "bash.exe");
        var config = await _configurationService.LoadConfigurationAsync(cancellationToken);

        workingDirectory ??= _configurationService.GetProjectRoot();

        // Set environment variables in current process so bash inherits them
        Environment.SetEnvironmentVariable("MSYSTEM", config.MSystem);
        Environment.SetEnvironmentVariable("CHERE_INVOKING", "1");

        // Convert Windows path separators to Unix-style for MSYS2 bash
        // This handles paths like "install-release\launcher.exe" -> "install-release/launcher.exe"
        var normalizedCommand = command.Replace('\\', '/');

        // Use cygpath inline to convert Windows path to MSYS2 path
        var args = $"-l -c \"cd $(cygpath -u '{workingDirectory}') && {normalizedCommand}\"";

        if (redirectOutput)
        {
            // Capture output programmatically
            var psi = new ProcessStartInfo
            {
                FileName = msysPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return -1;
            }

            // Consume output asynchronously to avoid deadlock
            var outputTask = Task.Run(() =>
            {
                var sb = new System.Text.StringBuilder();
                while (!process.HasExited && !cancellationToken.IsCancellationRequested)
                {
                    var line = process.StandardOutput.ReadLine();
                    if (line == null) break;
                    // Output is consumed but not used (caller can extend to capture if needed)
                }
                return Task.CompletedTask;
            }, cancellationToken);

            var errorTask = Task.Run(() =>
            {
                while (!process.HasExited && !cancellationToken.IsCancellationRequested)
                {
                    var line = process.StandardError.ReadLine();
                    if (line == null) break;
                    // Stderr is consumed but not used
                }
                return Task.CompletedTask;
            }, cancellationToken);

            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode;
        }
        else
        {
            // Let bash inherit the console (like PowerShell's & operator)
            var psi = new ProcessStartInfo
            {
                FileName = msysPath,
                Arguments = args,
                UseShellExecute = false
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return -1;
            }

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode;
        }
    }

    public async Task<int> ExecutePacmanAsync(string args, bool redirectOutput = false, CancellationToken cancellationToken = default)
    {
        return await ExecuteCommandAsync($"pacman --noconfirm {args}", redirectOutput: redirectOutput, cancellationToken: cancellationToken);
    }

    public async Task UpdatePackageListAsync(CancellationToken cancellationToken = default)
    {
        await ExecutePacmanAsync("-Sy", redirectOutput: false, cancellationToken);
    }

    public async Task UpgradeInstalledPackagesAsync(CancellationToken cancellationToken = default)
    {
        await ExecutePacmanAsync("-Su", redirectOutput: false, cancellationToken);
    }

    private async Task<string> ConvertToMsysPathAsync(string windowsPath, CancellationToken cancellationToken)
    {
        var msysRoot = GetMsys2Root();
        var msysBash = _fileSystem.Path.Combine(msysRoot, "msys64", "usr", "bin", "bash.exe");
        var config = await _configurationService.LoadConfigurationAsync(cancellationToken);

        // Set environment variables for this process so cygpath works correctly
        Environment.SetEnvironmentVariable("MSYSTEM", config.MSystem);
        Environment.SetEnvironmentVariable("CHERE_INVOKING", "1");

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

    private async Task DownloadFileAsync(Uri uri, string destination, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();

        // First check if we have a cached file
        if (_fileSystem.File.Exists(destination))
        {
            try
            {
                // Check the expected file size via HEAD request
                using var headResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, uri), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (headResponse.IsSuccessStatusCode)
                {
                    var expectedSize = headResponse.Content.Headers.ContentLength ?? 0;
                    var cachedSize = _fileSystem.FileInfo.New(destination).Length;

                    if (expectedSize > 0 && cachedSize == expectedSize)
                    {
                        // File is complete, use cached version
                        progress?.Report(new DownloadProgress
                        {
                            Percent = 0.5f,
                            DownloadedBytes = cachedSize,
                            TotalBytes = expectedSize,
                            SpeedBytesPerSecond = 0,
                            Eta = TimeSpan.Zero,
                            IsCached = true
                        });
                        return;
                    }
                }
            }
            catch
            {
                // If check fails, proceed with download
            }
        }

        // Download the file
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        var buffer = new byte[81920];

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = _fileSystem.File.Create(destination);

        var totalRead = 0L;
        var read = 0;
        var startTime = DateTime.UtcNow;
        var lastReportTime = startTime;

        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
            totalRead += read;

            var now = DateTime.UtcNow;
            // Update progress every 0.5 seconds
            if ((now - lastReportTime).TotalMilliseconds >= 500 && progress is not null)
            {
                var elapsed = (now - startTime).TotalSeconds;
                var speedBytesPerSecond = elapsed > 0 ? totalRead / elapsed : 0;
                var remainingBytes = totalBytes - totalRead;
                var eta = speedBytesPerSecond > 0 ? TimeSpan.FromSeconds(remainingBytes / speedBytesPerSecond) : (TimeSpan?)null;

                progress.Report(new DownloadProgress
                {
                    Percent = (float)totalRead / totalBytes * 0.5f, // Download is 50% of total
                    DownloadedBytes = totalRead,
                    TotalBytes = totalBytes,
                    SpeedBytesPerSecond = speedBytesPerSecond,
                    Eta = eta
                });
                lastReportTime = now;
            }
        }

        // Final progress report
        if (progress is not null)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
            var speedBytesPerSecond = elapsed > 0 ? totalRead / elapsed : 0;
            progress.Report(new DownloadProgress
            {
                Percent = 0.5f,
                DownloadedBytes = totalRead,
                TotalBytes = totalBytes,
                SpeedBytesPerSecond = speedBytesPerSecond,
                Eta = TimeSpan.Zero
            });
        }
    }

    private async Task ExtractArchiveAsync(string archivePath, string destination, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        // SharpCompress supports .tar.xz via ReaderFactory (not ArchiveFactory)
        // .xz is a compression stream, not an archive format
        await Task.Run(() =>
        {
            _fileSystem.Directory.CreateDirectory(destination);

            // First pass: count total entries (open new stream)
            var totalEntries = 0;
            using (var stream1 = _fileSystem.File.OpenRead(archivePath))
            using (var reader1 = ReaderFactory.Open(stream1))
            {
                while (reader1.MoveToNextEntry())
                {
                    if (!reader1.Entry.IsDirectory)
                        totalEntries++;
                }
            }

            // Second pass: extract (open new stream)
            using (var stream2 = _fileSystem.File.OpenRead(archivePath))
            using (var reader2 = ReaderFactory.Open(stream2))
            {
                var extracted = 0;
                var startTime = DateTime.UtcNow;
                var lastReportTime = startTime;

                while (reader2.MoveToNextEntry())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!reader2.Entry.IsDirectory)
                    {
                        reader2.WriteEntryToDirectory(destination, new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });

                        extracted++;

                        // Report progress: 0.5 to 1.0 (50% to 100%)
                        var now = DateTime.UtcNow;
                        if ((now - lastReportTime).TotalMilliseconds >= 500 && progress is not null && totalEntries > 0)
                        {
                            var percent = 0.5f + (float)extracted / totalEntries * 0.5f;
                            var elapsed = (now - startTime).TotalSeconds;
                            var speedPerSecond = elapsed > 0 ? extracted / elapsed : 0;
                            var remainingEntries = totalEntries - extracted;
                            var eta = speedPerSecond > 0 ? TimeSpan.FromSeconds(remainingEntries / speedPerSecond) : (TimeSpan?)null;

                            progress.Report(new DownloadProgress
                            {
                                Percent = percent,
                                DownloadedBytes = extracted,
                                TotalBytes = totalEntries,
                                SpeedBytesPerSecond = speedPerSecond,
                                Eta = eta
                            });
                            lastReportTime = now;
                        }
                    }
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
