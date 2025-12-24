using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Configuration;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class BootstrapCommand : Command
{
    public readonly Option<string?> UrlOption;

    public BootstrapCommand() : base("bootstrap", "Install MSYS2")
    {
        UrlOption = new Option<string?>(
            ["--url", "-u"],
            getDefaultValue: () => null,
            description: "Override the download URL (defaults to using config version)"
        );

        AddOption(UrlOption);
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

        public string? Url { get; set; }

        public int Invoke(InvocationContext context)
        {
            return InvokeAsync(context).GetAwaiter().GetResult();
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var console = context.Console;

            if (_environment.IsMsys2Installed())
            {
                console.Out.Write("MSYS2 is already installed.\n");
                return 0;
            }

            console.Out.Write("Installing MSYS2...\n");

            var progress = new Progress<DownloadProgress>(p =>
            {
                var status = p.Percent < 0.5f ? "Downloading" : "Extracting";
                if (p.Percent < 0.5f)
                {
                    // Download phase - show progress bar with speed and ETA
                    console.Out.Write($"\r{status}: {p.GetProgressDisplay(25)}".PadRight(120));
                }
                else
                {
                    // Extract phase - show progress bar with file count and ETA
                    var extractPercent = (p.Percent - 0.5f) * 2; // Convert 0.5-1.0 to 0-100%
                    var barWidth = 25;
                    var filled = (int)Math.Round(extractPercent * barWidth);
                    var empty = barWidth - filled;
                    var bar = $"[{new string('█', filled)}{new string('░', empty)}] {extractPercent * 100:F1}%";
                    console.Out.Write($"\r{status}: {bar} | {p.DownloadedBytes}/{p.TotalBytes} files | ETA: {p.FormattedEta}".PadRight(120));
                }
            });

            try
            {
                // Use provided URL or construct from config
                Uri downloadUrl;
                Msys2Configuration? config = null;

                if (!string.IsNullOrEmpty(Url))
                {
                    downloadUrl = new Uri(Url);
                }
                else
                {
                    // Read from config to get BaseUrl and construct download URL
                    config = await _configuration.LoadConfigurationAsync(context.GetCancellationToken());
                    var baseUrl = new Uri(config.BaseUrl ?? "https://repo.msys2.org/distrib/x86_64/");
                    var versionForUrl = config.Version?.Replace("-", "") ?? "20251213";
                    // Use .tar.xz instead of .tar.zst since SharpCompress doesn't support zstd
                    downloadUrl = new Uri(baseUrl, $"msys2-base-x86_64-{versionForUrl}.tar.xz");
                }

                await _environment.InstallMsys2Async(downloadUrl, progress, context.GetCancellationToken());
                console.Out.Write("\nMSYS2 installed successfully.\n");

                // Load config again for AutoUpdate check
                config ??= await _configuration.LoadConfigurationAsync(context.GetCancellationToken());

                if (config.AutoUpdate)
                {
                    console.Out.Write("Updating packages...\n");
                    await _environment.UpgradeInstalledPackagesAsync(context.GetCancellationToken());
                }

                return 0;
            }
            catch (Exception ex)
            {
                console.Error.Write($"\nError: {ex.Message}\n");
                return 1;
            }
        }
    }
}
