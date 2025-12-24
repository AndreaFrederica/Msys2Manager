using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class SearchCommand : Command
{
    public readonly Argument<string[]> QueryArgument;

    public SearchCommand() : base("search", "Search for packages in MSYS2 repositories")
    {
        QueryArgument = new Argument<string[]>("query", "Search query (one or more terms)");
        AddArgument(QueryArgument);
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

        public string[] Query { get; set; } = Array.Empty<string>();

        public int Invoke(InvocationContext context)
        {
            return InvokeAsync(context).GetAwaiter().GetResult();
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var console = context.Console;

            if (!_environment.IsMsys2Installed())
            {
                console.Error.Write("MSYS2 is not installed. Run 'm2m bootstrap' first.\n");
                return 1;
            }

            if (Query.Length == 0)
            {
                console.Error.Write("Error: Search query is required.\n");
                return 1;
            }

            var searchTerm = string.Join(" ", Query);
            console.Out.Write($"Searching for \"{searchTerm}\"...\n\n");

            try
            {
                var results = await SearchPackagesAsync(searchTerm, context.GetCancellationToken());

                if (results.Count == 0)
                {
                    console.Out.Write("No packages found.\n");
                    return 0;
                }

                foreach (var result in results)
                {
                    console.Out.Write($"{result}\n");
                }

                console.Out.Write($"\nFound {results.Count} package(s).\n");
                return 0;
            }
            catch (Exception ex)
            {
                console.Error.Write($"Error: {ex.Message}\n");
                return 1;
            }
        }

        private async Task<List<string>> SearchPackagesAsync(string query, CancellationToken cancellationToken)
        {
            var msysRoot = _environment.GetMsys2Root();
            var pacman = System.IO.Path.Combine(msysRoot, "msys64", "usr", "bin", "pacman.exe");
            var config = await _configuration.LoadConfigurationAsync(cancellationToken);

            System.Environment.SetEnvironmentVariable("MSYSTEM", config.MSystem);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pacman,
                Arguments = $"-Ss {query}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null)
                return new List<string>();

            var results = new List<string>();
            while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    results.Add(line);
                }
            }

            await process.WaitForExitAsync(cancellationToken);
            return results;
        }
    }
}
