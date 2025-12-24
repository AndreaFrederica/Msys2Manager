using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class ShellCommand : Command
{
    public ShellCommand() : base("shell", "Start an interactive MSYS2 shell")
    {
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

        public int Invoke(InvocationContext context)
        {
            return InvokeAsync(context).GetAwaiter().GetResult();
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            if (!_environment.IsMsys2Installed())
            {
                context.Console.Error.Write("MSYS2 is not installed. Run 'm2m bootstrap' first.\n");
                return 1;
            }

            var msysRoot = _environment.GetMsys2Root();
            var config = await _configuration.LoadConfigurationAsync(context.GetCancellationToken());
            var projectRoot = _configuration.GetProjectRoot();

            var msysBat = System.IO.Path.Combine(msysRoot, "msys64", "msys2.exe");

            var startInfo = new System.Diagnostics.ProcessStartInfo
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

            System.Diagnostics.Process.Start(startInfo)?.WaitForExit();
            return 0;
        }
    }
}
