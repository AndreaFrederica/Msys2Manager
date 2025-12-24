using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class HelpCommand : Command
{
    public readonly Argument<string?> CommandArgument;

    public HelpCommand() : base("help", "Show help information for all commands")
    {
        CommandArgument = new Argument<string?>("command", "The command to get help for (optional)");
        AddArgument(CommandArgument);
    }

    public new class Handler : ICommandHandler
    {
        public string? Command { get; set; }

        public int Invoke(InvocationContext context)
        {
            var console = context.Console;

            if (string.IsNullOrWhiteSpace(Command))
            {
                // Show general help
                ShowGeneralHelp(console);
                return 0;
            }

            // Show help for specific command
            ShowCommandHelp(console, Command!);
            return 0;
        }

        public Task<int> InvokeAsync(InvocationContext context)
        {
            return Task.FromResult(Invoke(context));
        }

        private void ShowGeneralHelp(IConsole console)
        {
            console.Out.Write("M2M - MSYS2 Management Tool\n\n");
            console.Out.Write("USAGE:\n");
            console.Out.Write("  m2m [command] [options]\n\n");
            console.Out.Write("COMMANDS:\n");
            console.Out.Write("  init         Initialize a new MSYS2 environment in the current directory\n");
            console.Out.Write("  bootstrap    Install MSYS2\n");
            console.Out.Write("  update       Update all MSYS2 packages\n");
            console.Out.Write("  sync         Sync installed packages with configuration\n");
            console.Out.Write("  add          Add a package to configuration and install it\n");
            console.Out.Write("  remove       Remove a package from configuration\n");
            console.Out.Write("  run          Run a task or command\n");
            console.Out.Write("  shell        Start an interactive MSYS2 shell\n");
            console.Out.Write("  clean        Remove MSYS2 environment\n");
            console.Out.Write("  help         Show help information for all commands\n\n");
            console.Out.Write("OPTIONS:\n");
            console.Out.Write("  -h, --help    Show help and exit\n\n");
            console.Out.Write("EXAMPLES:\n");
            console.Out.Write("  m2m init                     Initialize with interactive prompts\n");
            console.Out.Write("  m2m init -l                   List available MSYS2 versions\n");
            console.Out.Write("  m2m init -v 2024-01-13 -m CLANG64  Initialize with specific version and system\n");
            console.Out.Write("  m2m bootstrap                 Install MSYS2\n");
            console.Out.Write("  m2m add cmake ninja           Install cmake and ninja packages\n");
            console.Out.Write("  m2m shell                     Open MSYS2 shell\n");
            console.Out.Write("  m2m run build                 Run the 'build' task\n\n");
            console.Out.Write("For more information about a command, run:\n");
            console.Out.Write("  m2m help [command]\n");
        }

        private void ShowCommandHelp(IConsole console, string command)
        {
            console.Out.Write($"\nHelp for '{command}':\n\n");

            switch (command.ToLower())
            {
                case "init":
                    console.Out.Write("USAGE:\n");
                    console.Out.Write("  m2m init [options]\n\n");
                    console.Out.Write("OPTIONS:\n");
                    console.Out.Write("  -m, --msystem <SYSTEM>      The MSYS2 system to use (UCRT64, CLANG64, MINGW64, MINGW32, CLANG32)\n");
                    console.Out.Write("                              If not specified, you will be prompted interactively\n");
                    console.Out.Write("  --mirror <URL>              Mirror URL for package downloads\n");
                    console.Out.Write("  -v, --version <VERSION>      MSYS2 version (e.g., 2024-01-13). Defaults to latest\n");
                    console.Out.Write("  -l, --list-versions         List available MSYS2 versions\n");
                    console.Out.Write("  -h, --help                  Show help\n\n");
                    console.Out.Write("DESCRIPTION:\n");
                    console.Out.Write("  Initialize a new MSYS2 environment in the current directory.\n");
                    console.Out.Write("  Creates msys2.toml configuration file with your chosen settings.\n\n");
                    console.Out.Write("EXAMPLES:\n");
                    console.Out.Write("  m2m init                    Interactive initialization\n");
                    console.Out.Write("  m2m init -l                 List available versions\n");
                    console.Out.Write("  m2m init -m CLANG64         Choose CLANG64 system\n");
                    console.Out.Write("  m2m init -v 2024-01-13       Use specific version\n");
                    break;

                case "bootstrap":
                    console.Out.Write("USAGE:\n");
                    console.Out.Write("  m2m bootstrap [options]\n\n");
                    console.Out.Write("OPTIONS:\n");
                    console.Out.Write("  -u, --url <URL>    Base URL for MSYS2 installation\n");
                    console.Out.Write("                      Default: https://github.com/msys2/msys2-installer/releases/download/2024-01-13/\n");
                    console.Out.Write("  -h, --help        Show help\n\n");
                    console.Out.Write("DESCRIPTION:\n");
                    console.Out.Write("  Download and install MSYS2 to the configured location.\n");
                    console.Out.Write("  If AutoUpdate is enabled in msys2.toml, packages will be updated after installation.\n\n");
                    console.Out.Write("EXAMPLES:\n");
                    console.Out.Write("  m2m bootstrap               Install with default settings\n");
                    console.Out.Write("  m2m bootstrap -u <url>      Install from custom URL\n");
                    break;

                case "update":
                    console.Out.Write("USAGE:\n");
                    console.Out.Write("  m2m update\n\n");
                    console.Out.Write("DESCRIPTION:\n");
                    console.Out.Write("  Update all MSYS2 packages to their latest versions.\n");
                    console.Out.Write("  This updates the package database and all installed packages.\n");
                    break;

                case "sync":
                    console.Out.Write("USAGE:\n");
                    console.Out.Write("  m2m sync [options]\n\n");
                    console.Out.Write("OPTIONS:\n");
                    console.Out.Write("  -p, --prune    Remove packages not in configuration\n");
                    console.Out.Write("  -h, --help    Show help\n\n");
                    console.Out.Write("DESCRIPTION:\n");
                    console.Out.Write("  Sync installed packages with the configuration in msys2.toml.\n");
                    console.Out.Write("  Installs missing packages and optionally removes extra packages.\n\n");
                    console.Out.Write("EXAMPLES:\n");
                    console.Out.Write("  m2m sync            Install missing packages\n");
                    console.Out.Write("  m2m sync -p         Install missing and remove extra\n");
                    break;

                case "add":
                    console.Out.Write("USAGE:\n");
                    console.Out.Write("  m2m add <packages> [options]\n\n");
                    console.Out.Write("ARGUMENTS:\n");
                    console.Out.Write("  <packages>    One or more package names to add\n\n");
                    console.Out.Write("OPTIONS:\n");
                    console.Out.Write("  -v, --version <VERSION>    Version constraint (e.g., '6.6.*', '1.0.0')\n");
                    console.Out.Write("  -h, --help                Show help\n\n");
                    console.Out.Write("DESCRIPTION:\n");
                    console.Out.Write("  Add packages to configuration and install them.\n");
                    console.Out.Write("  Packages are added to msys2.toml and installed immediately.\n\n");
                    console.Out.Write("EXAMPLES:\n");
                    console.Out.Write("  m2m add cmake               Install cmake\n");
                    console.Out.Write("  m2m add cmake ninja         Install cmake and ninja\n");
                    console.Out.Write("  m2m add gcc -v 14.2.0        Install gcc 14.2.0\n");
                    break;

                case "remove":
                    console.Out.Write("USAGE:\n");
                    console.Out.Write("  m2m remove <packages>\n\n");
                    console.Out.Write("ARGUMENTS:\n");
                    console.Out.Write("  <packages>    One or more package names to remove\n\n");
                    console.Out.Write("DESCRIPTION:\n");
                    console.Out.Write("  Remove packages from configuration.\n");
                    console.Out.Write("  Packages are removed from msys2.toml but NOT uninstalled.\n\n");
                    console.Out.Write("EXAMPLES:\n");
                    console.Out.Write("  m2m remove gcc               Remove gcc from config\n");
                    console.Out.Write("  m2m remove gcc ninja         Remove gcc and ninja\n");
                    break;

                case "run":
                    console.Out.Write("USAGE:\n");
                    console.Out.Write("  m2m run [options] [task]\n\n");
                    console.Out.Write("OPTIONS:\n");
                    console.Out.Write("  -l, --list    List available tasks defined in msys2.toml\n");
                    console.Out.Write("  -h, --help    Show help\n\n");
                    console.Out.Write("ARGUMENTS:\n");
                    console.Out.Write("  <task>        Task name or shell command to run\n\n");
                    console.Out.Write("DESCRIPTION:\n");
                    console.Out.Write("  Run a task defined in msys2.toml or execute a shell command.\n");
                    console.Out.Write("  Tasks are defined in the [tasks] section of msys2.toml.\n\n");
                    console.Out.Write("EXAMPLES:\n");
                    console.Out.Write("  m2m run -l                   List available tasks\n");
                    console.Out.Write("  m2m run build                Run 'build' task\n");
                    console.Out.Write("  m2m run 'cmake ..'            Run shell command\n");
                    break;

                case "shell":
                    console.Out.Write("USAGE:\n");
                    console.Out.Write("  m2m shell\n\n");
                    console.Out.Write("DESCRIPTION:\n");
                    console.Out.Write("  Start an interactive MSYS2 shell in the project directory.\n");
                    console.Out.Write("  The shell uses the MSystem configured in msys2.toml.\n\n");
                    console.Out.Write("EXAMPLES:\n");
                    console.Out.Write("  m2m shell                   Open MSYS2 shell\n");
                    break;

                case "clean":
                    console.Out.Write("USAGE:\n");
                    console.Out.Write("  m2m clean [options]\n\n");
                    console.Out.Write("OPTIONS:\n");
                    console.Out.Write("  -f, --force    Skip confirmation prompt\n");
                    console.Out.Write("  -h, --help    Show help\n\n");
                    console.Out.Write("DESCRIPTION:\n");
                    console.Out.Write("  Remove the MSYS2 environment completely.\n");
                    console.Out.Write("  This deletes the MSYS2 installation directory.\n\n");
                    console.Out.Write("EXAMPLES:\n");
                    console.Out.Write("  m2m clean                    Clean with confirmation\n");
                    console.Out.Write("  m2m clean -f                 Force clean without confirmation\n");
                    break;

                default:
                    console.Error.Write($"Unknown command: {command}\n");
                    console.Error.Write("Run 'm2m help' to see all available commands.\n");
                    break;
            }
        }
    }
}
