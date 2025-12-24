using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO.Abstractions;

namespace Msys2Manager.CLI.Commands;

public static class CommandExtensions
{
    public static void AddCommands(this RootCommand rootCommand, IServiceProvider services)
    {
        // Help command
        var help = new HelpCommand();
        help.SetHandler<string?>(async (command) =>
        {
            var console = Console.Out;

            if (string.IsNullOrWhiteSpace(command))
            {
                // Show general help
                console.Write("M2M - MSYS2 Management Tool\n\n");
                console.Write("USAGE:\n");
                console.Write("  m2m [command] [options]\n\n");
                console.Write("COMMANDS:\n");
                console.Write("  init         Initialize a new MSYS2 environment in the current directory\n");
                console.Write("  bootstrap    Install MSYS2\n");
                console.Write("  update       Update all MSYS2 packages\n");
                console.Write("  sync         Sync installed packages with configuration\n");
                console.Write("  add          Add a package to configuration and install it\n");
                console.Write("  remove       Remove a package from configuration\n");
                console.Write("  run          Run a task or command\n");
                console.Write("  shell        Start an interactive MSYS2 shell\n");
                console.Write("  clean        Remove MSYS2 environment\n");
                console.Write("  help         Show help information for all commands\n\n");
                console.Write("OPTIONS:\n");
                console.Write("  -h, --help    Show help and exit\n\n");
                console.Write("EXAMPLES:\n");
                console.Write("  m2m init                     Initialize with interactive prompts\n");
                console.Write("  m2m init -l                   List available MSYS2 versions\n");
                console.Write("  m2m init -v 2024-01-13 -m CLANG64  Initialize with specific version and system\n");
                console.Write("  m2m bootstrap                 Install MSYS2\n");
                console.Write("  m2m add cmake ninja           Install cmake and ninja packages\n");
                console.Write("  m2m shell                     Open MSYS2 shell\n");
                console.Write("  m2m run build                 Run the 'build' task\n\n");
                console.Write("For more information about a command, run:\n");
                console.Write("  m2m help [command]\n");
                Environment.ExitCode = 0;
                return;
            }

            console.Write($"\nHelp for '{command}':\n\n");

            switch (command.ToLower())
            {
                case "init":
                    console.Write("USAGE:\n");
                    console.Write("  m2m init [options]\n\n");
                    console.Write("OPTIONS:\n");
                    console.Write("  -m, --msystem <SYSTEM>      The MSYS2 system to use (UCRT64, CLANG64, MINGW64, MINGW32, CLANG32)\n");
                    console.Write("                              If not specified, you will be prompted interactively\n");
                    console.Write("  --mirror <URL>              Mirror URL for package downloads\n");
                    console.Write("  -v, --version <VERSION>      MSYS2 version (e.g., 2024-01-13). Defaults to latest\n");
                    console.Write("  -l, --list-versions         List available MSYS2 versions\n");
                    console.Write("  -h, --help                  Show help\n\n");
                    console.Write("DESCRIPTION:\n");
                    console.Write("  Initialize a new MSYS2 environment in the current directory.\n");
                    console.Write("  Creates msys2.toml configuration file with your chosen settings.\n\n");
                    console.Write("EXAMPLES:\n");
                    console.Write("  m2m init                    Interactive initialization\n");
                    console.Write("  m2m init -l                 List available versions\n");
                    console.Write("  m2m init -m CLANG64         Choose CLANG64 system\n");
                    console.Write("  m2m init -v 2024-01-13       Use specific version\n");
                    break;

                case "bootstrap":
                    console.Write("USAGE:\n");
                    console.Write("  m2m bootstrap [options]\n\n");
                    console.Write("OPTIONS:\n");
                    console.Write("  -u, --url <URL>    Base URL for MSYS2 installation\n");
                    console.Write("                      Default: https://github.com/msys2/msys2-installer/releases/download/2024-01-13/\n");
                    console.Write("  -h, --help        Show help\n\n");
                    console.Write("DESCRIPTION:\n");
                    console.Write("  Download and install MSYS2 to the configured location.\n");
                    console.Write("  If AutoUpdate is enabled in msys2.toml, packages will be updated after installation.\n\n");
                    console.Write("EXAMPLES:\n");
                    console.Write("  m2m bootstrap               Install with default settings\n");
                    console.Write("  m2m bootstrap -u <url>      Install from custom URL\n");
                    break;

                case "update":
                    console.Write("USAGE:\n");
                    console.Write("  m2m update\n\n");
                    console.Write("DESCRIPTION:\n");
                    console.Write("  Update all MSYS2 packages to their latest versions.\n");
                    console.Write("  This updates the package database and all installed packages.\n");
                    break;

                case "sync":
                    console.Write("USAGE:\n");
                    console.Write("  m2m sync [options]\n\n");
                    console.Write("OPTIONS:\n");
                    console.Write("  -p, --prune    Remove packages not in configuration\n");
                    console.Write("  -h, --help    Show help\n\n");
                    console.Write("DESCRIPTION:\n");
                    console.Write("  Sync installed packages with the configuration in msys2.toml.\n");
                    console.Write("  Installs missing packages and optionally removes extra packages.\n\n");
                    console.Write("EXAMPLES:\n");
                    console.Write("  m2m sync            Install missing packages\n");
                    console.Write("  m2m sync -p         Install missing and remove extra\n");
                    break;

                case "add":
                    console.Write("USAGE:\n");
                    console.Write("  m2m add <packages> [options]\n\n");
                    console.Write("ARGUMENTS:\n");
                    console.Write("  <packages>    One or more package names to add\n\n");
                    console.Write("OPTIONS:\n");
                    console.Write("  -v, --version <VERSION>    Version constraint (e.g., '6.6.*', '1.0.0')\n");
                    console.Write("  -h, --help                Show help\n\n");
                    console.Write("DESCRIPTION:\n");
                    console.Write("  Add packages to configuration and install them.\n");
                    console.Write("  Packages are added to msys2.toml and installed immediately.\n\n");
                    console.Write("EXAMPLES:\n");
                    console.Write("  m2m add cmake               Install cmake\n");
                    console.Write("  m2m add cmake ninja         Install cmake and ninja\n");
                    console.Write("  m2m add gcc -v 14.2.0        Install gcc 14.2.0\n");
                    break;

                case "remove":
                    console.Write("USAGE:\n");
                    console.Write("  m2m remove <packages>\n\n");
                    console.Write("ARGUMENTS:\n");
                    console.Write("  <packages>    One or more package names to remove\n\n");
                    console.Write("DESCRIPTION:\n");
                    console.Write("  Remove packages from configuration.\n");
                    console.Write("  Packages are removed from msys2.toml but NOT uninstalled.\n\n");
                    console.Write("EXAMPLES:\n");
                    console.Write("  m2m remove gcc               Remove gcc from config\n");
                    console.Write("  m2m remove gcc ninja         Remove gcc and ninja\n");
                    break;

                case "run":
                    console.Write("USAGE:\n");
                    console.Write("  m2m run [options] [task]\n\n");
                    console.Write("OPTIONS:\n");
                    console.Write("  -l, --list    List available tasks defined in msys2.toml\n");
                    console.Write("  -h, --help    Show help\n\n");
                    console.Write("ARGUMENTS:\n");
                    console.Write("  <task>        Task name or shell command to run\n\n");
                    console.Write("DESCRIPTION:\n");
                    console.Write("  Run a task defined in msys2.toml or execute a shell command.\n");
                    console.Write("  Tasks are defined in the [tasks] section of msys2.toml.\n\n");
                    console.Write("EXAMPLES:\n");
                    console.Write("  m2m run -l                   List available tasks\n");
                    console.Write("  m2m run build                Run 'build' task\n");
                    console.Write("  m2m run 'cmake ..'            Run shell command\n");
                    break;

                case "shell":
                    console.Write("USAGE:\n");
                    console.Write("  m2m shell\n\n");
                    console.Write("DESCRIPTION:\n");
                    console.Write("  Start an interactive MSYS2 shell in the project directory.\n");
                    console.Write("  The shell uses the MSystem configured in msys2.toml.\n\n");
                    console.Write("EXAMPLES:\n");
                    console.Write("  m2m shell                   Open MSYS2 shell\n");
                    break;

                case "clean":
                    console.Write("USAGE:\n");
                    console.Write("  m2m clean [options]\n\n");
                    console.Write("OPTIONS:\n");
                    console.Write("  -f, --force    Skip confirmation prompt\n");
                    console.Write("  -h, --help    Show help\n\n");
                    console.Write("DESCRIPTION:\n");
                    console.Write("  Remove the MSYS2 environment completely.\n");
                    console.Write("  This deletes the MSYS2 installation directory.\n\n");
                    console.Write("EXAMPLES:\n");
                    console.Write("  m2m clean                    Clean with confirmation\n");
                    console.Write("  m2m clean -f                 Force clean without confirmation\n");
                    break;

                default:
                    Console.Error.Write($"Unknown command: {command}\n");
                    Console.Error.Write("Run 'm2m help' to see all available commands.\n");
                    Environment.ExitCode = 1;
                    break;
            }
        },
        help.CommandArgument);
        rootCommand.AddCommand(help);

        // Init command
        var init = new InitCommand();
        init.SetHandler<string?, string?, string?, bool>(async (msystem, mirror, version, listVersions) =>
        {
            var fs = services.GetRequiredService<IFileSystem>();
            var confService = services.GetRequiredService<Msys2Manager.Core.Services.IConfigurationService>();
            var versionService = services.GetRequiredService<Msys2Manager.Core.Services.IVersionService>();

            // Handle --list-versions option
            if (listVersions)
            {
                Console.Out.Write("Fetching available MSYS2 versions...\n");
                var versions = await versionService.GetAvailableVersionsAsync();

                Console.Out.Write($"\nAvailable versions ({versions.Length} total):\n");
                foreach (var v in versions.Take(20))
                {
                    Console.Out.Write($"  {v}\n");
                }
                if (versions.Length > 20)
                {
                    Console.Out.Write($"  ... and {versions.Length - 20} more\n");
                }
                Console.Out.Write($"\nLatest version: {versions.FirstOrDefault() ?? "unknown"}\n");
                Environment.ExitCode = 0;
                return;
            }

            var config = services.GetRequiredService<Msys2Manager.Core.Configuration.Msys2Configuration>();
            var projectRoot = confService.GetProjectRoot();
            var configPath = fs.Path.Combine(projectRoot, "msys2.toml");

            if (fs.File.Exists(configPath))
            {
                Console.Error.Write($"Error: msys2.toml already exists at {projectRoot}\n");
                Console.Error.Write("To reinitialize, delete the existing file first.\n");
                Environment.ExitCode = 1;
                return;
            }

            // Get version - use specified version or fetch latest
            var msys2Version = version;
            if (string.IsNullOrEmpty(msys2Version))
            {
                Console.Out.Write("Fetching latest MSYS2 version...\n");
                msys2Version = await versionService.GetLatestVersionAsync();
                Console.Out.Write($"Using latest version: {msys2Version}\n");
            }

            // Interactive MSYS2 system selection if not specified
            var selectedMsystem = msystem;
            if (string.IsNullOrEmpty(selectedMsystem))
            {
                Console.Out.Write("\nSelect MSYS2 system:\n");
                Console.Out.Write("  1. UCRT64      (Recommended, uses new UCRT runtime)\n");
                Console.Out.Write("  2. CLANG64     (Clang/LLVM toolchain)\n");
                Console.Out.Write("  3. MINGW64      (GCC toolchain, Win64 threading)\n");
                Console.Out.Write("  4. MINGW32      (GCC toolchain, Win32 threading)\n");
                Console.Out.Write("  5. CLANG32     (Clang/LLVM toolchain, Win32 threading)\n");
                Console.Out.Write("\nYour choice [1-5, default: 1]: ");

                var input = Console.ReadLine();
                int choice;
                if (string.IsNullOrWhiteSpace(input) || !int.TryParse(input, out choice) || choice < 1 || choice > 5)
                {
                    choice = 1; // Default to UCRT64
                }

                selectedMsystem = choice switch
                {
                    1 => "UCRT64",
                    2 => "CLANG64",
                    3 => "MINGW64",
                    4 => "MINGW32",
                    5 => "CLANG32",
                    _ => "UCRT64"
                };
                Console.Out.Write($"Selected: {selectedMsystem}\n");
            }

            config.MSystem = selectedMsystem;
            config.Mirror = mirror;
            config.BaseUrl = $"https://github.com/msys2/msys2-installer/releases/download/{msys2Version}/";
            config.AutoUpdate = true;

            await confService.SaveConfigurationAsync(config);

            Console.Out.Write($"\nInitialized MSYS2 environment at {projectRoot}\n");
            Console.Out.Write($"  Version: {msys2Version}\n");
            Console.Out.Write($"  MSystem: {config.MSystem}\n");
            if (config.Mirror != null)
            {
                Console.Out.Write($"  Mirror: {config.Mirror}\n");
            }
            Console.Out.Write("\n");
            Console.Out.Write("Next steps:\n");
            Console.Out.Write("  1. Run 'm2m bootstrap' to install MSYS2\n");
            Console.Out.Write("  2. Run 'm2m add <package>' to add packages\n");
            Console.Out.Write("  3. Run 'm2m shell' to open a shell\n");

            Environment.ExitCode = 0;
        },
        init.MSystemOption,
        init.MirrorOption,
        init.VersionOption,
        init.ListVersionsOption);
        rootCommand.AddCommand(init);

        // Bootstrap command
        var bootstrap = new BootstrapCommand();
        bootstrap.SetHandler<string>(async (url) =>
        {
            var env = services.GetRequiredService<Msys2Manager.Core.Services.IEnvironmentService>();
            var conf = services.GetRequiredService<Msys2Manager.Core.Services.IConfigurationService>();

            if (env.IsMsys2Installed())
            {
                Console.Out.Write("MSYS2 is already installed.\n");
                Environment.ExitCode = 0;
            }

            Console.Out.Write("Installing MSYS2...\n");
            await env.InstallMsys2Async(new Uri(url), new Progress<float>(p => Console.Out.Write($"\rProgress: {p * 100:F1}%")));
            Console.Out.Write("\nMSYS2 installed successfully.\n");

            var config = await conf.LoadConfigurationAsync();
            if (config.AutoUpdate)
            {
                Console.Out.Write("Updating packages...\n");
                await env.UpdateAllPackagesAsync();
            }

            Environment.ExitCode = 0;
        },
        bootstrap.UrlOption);
        rootCommand.AddCommand(bootstrap);

        // Update command
        var update = new UpdateCommand();
        update.SetHandler(async () =>
        {
            var env = services.GetRequiredService<Msys2Manager.Core.Services.IEnvironmentService>();
            var conf = services.GetRequiredService<Msys2Manager.Core.Services.IConfigurationService>();

            if (!env.IsMsys2Installed())
            {
                Console.Error.Write("MSYS2 is not installed. Run 'm2m bootstrap' first.\n");
                Environment.ExitCode = 1;
            }

            Console.Out.Write("Updating MSYS2 packages...\n");
            await env.UpdateAllPackagesAsync();

            var lockFile = await conf.LoadLockFileAsync();
            await conf.SaveLockFileAsync(lockFile);

            Console.Out.Write("Update complete.\n");
            Environment.ExitCode = 0;
        });
        rootCommand.AddCommand(update);

        // Sync command
        var sync = new SyncCommand();
        sync.SetHandler<bool>(async (prune) =>
        {
            var packages = services.GetRequiredService<Msys2Manager.Core.Services.IPackageService>();
            var env = services.GetRequiredService<Msys2Manager.Core.Services.IEnvironmentService>();

            if (!env.IsMsys2Installed())
            {
                Console.Error.Write("MSYS2 is not installed. Run 'm2m bootstrap' first.\n");
                Environment.ExitCode = 1;
            }

            Console.Out.Write("Syncing packages...\n");
            await packages.SyncPackagesAsync(prune);
            Console.Out.Write("Sync complete.\n");
            Environment.ExitCode = 0;
        },
        sync.PruneOption);
        rootCommand.AddCommand(sync);

        // Add command
        var add = new AddCommand();
        add.SetHandler<string[], string?>(async (packages, version) =>
        {
            var pkgService = services.GetRequiredService<Msys2Manager.Core.Services.IPackageService>();
            var env = services.GetRequiredService<Msys2Manager.Core.Services.IEnvironmentService>();

            if (!env.IsMsys2Installed())
            {
                Console.Error.Write("MSYS2 is not installed. Run 'm2m bootstrap' first.\n");
                Environment.ExitCode = 1;
            }

            foreach (var package in packages)
            {
                await pkgService.AddPackageToConfigAsync(package, version);
                await pkgService.InstallPackageAsync(package, version);
            }
            Environment.ExitCode = 0;
        },
        add.PackagesArgument,
        add.VersionOption);
        rootCommand.AddCommand(add);

        // Remove command
        var remove = new RemoveCommand();
        remove.SetHandler<string[]>(async (packages) =>
        {
            var pkgService = services.GetRequiredService<Msys2Manager.Core.Services.IPackageService>();

            foreach (var package in packages)
            {
                await pkgService.RemovePackageFromConfigAsync(package);
            }
            Environment.ExitCode = 0;
        },
        remove.PackagesArgument);
        rootCommand.AddCommand(remove);

        // Run command
        var run = new RunCommand();
        run.SetHandler<bool, string?>(async (list, task) =>
        {
            var taskService = services.GetRequiredService<Msys2Manager.Core.Services.ITaskService>();
            var env = services.GetRequiredService<Msys2Manager.Core.Services.IEnvironmentService>();

            if (!env.IsMsys2Installed())
            {
                Console.Error.Write("MSYS2 is not installed. Run 'm2m bootstrap' first.\n");
                Environment.ExitCode = 1;
            }

            if (list)
            {
                var tasks = await taskService.GetTasksAsync();
                Console.Out.Write("Available tasks:\n");
                foreach (var (name, t) in tasks)
                {
                    var desc = t.Description ?? string.Empty;
                    Console.Out.Write($"  {name,-20} {desc}\n");
                }
                Environment.ExitCode = 0;
            }

            if (string.IsNullOrWhiteSpace(task))
            {
                Console.Error.Write("Error: No task or command specified.\n");
                Environment.ExitCode = 1;
            }

            var taskDict = await taskService.GetTasksAsync();
            if (taskDict.ContainsKey(task!))
            {
                Environment.ExitCode = await taskService.RunTaskAsync(task!);
            }
            else
            {
                Environment.ExitCode = await taskService.RunCommandAsync(task!);
            }
        },
        run.ListOption,
        run.TaskArgument);
        rootCommand.AddCommand(run);

        // Shell command
        var shell = new ShellCommand();
        shell.SetHandler(async () =>
        {
            var env = services.GetRequiredService<Msys2Manager.Core.Services.IEnvironmentService>();
            var conf = services.GetRequiredService<Msys2Manager.Core.Services.IConfigurationService>();

            if (!env.IsMsys2Installed())
            {
                Console.Error.Write("MSYS2 is not installed. Run 'm2m bootstrap' first.\n");
                Environment.ExitCode = 1;
            }

            var msysRoot = env.GetMsys2Root();
            var config = await conf.LoadConfigurationAsync();
            var projectRoot = conf.GetProjectRoot();

            var msysBat = System.IO.Path.Combine(msysRoot, "msys64", "msys2.exe");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = msysBat,
                Arguments = $"-shell {config.MSystem.ToLower()}",
                UseShellExecute = true,
                WorkingDirectory = projectRoot,
                Environment =
                {
                    ["MSYSTEM"] = config.MSystem,
                    ["CHERE_INVOKING"] = "1"
                }
            };

            System.Diagnostics.Process.Start(psi)?.WaitForExit();
            Environment.ExitCode = 0;
        });
        rootCommand.AddCommand(shell);

        // Clean command
        var clean = new CleanCommand();
        clean.SetHandler<bool>(async (force) =>
        {
            var env = services.GetRequiredService<Msys2Manager.Core.Services.IEnvironmentService>();

            if (!env.IsMsys2Installed())
            {
                Console.Out.Write("MSYS2 is not installed.\n");
                Environment.ExitCode = 0;
            }

            await env.RemoveMsys2Async(force);
            Console.Out.Write("MSYS2 environment removed.\n");
            Environment.ExitCode = 0;
        },
        clean.ForceOption);
        rootCommand.AddCommand(clean);
    }
}
