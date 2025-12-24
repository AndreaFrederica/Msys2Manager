using Microsoft.Extensions.DependencyInjection;
using Msys2Manager.Core.Services;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Msys2Manager.CLI.Commands;

public class RunCommand : Command
{
    public RunCommand() : base("run", "Run a task or command")
    {
        var listOption = new Option<bool>(
            ["--list", "-l"],
            "List available tasks"
        );

        AddOption(listOption);
        AddArgument(new Argument<string?>("task", "Task name or command to run") { Arity = ArgumentArity.ZeroOrOne });
    }

    public new class Handler : ICommandHandler
    {
        private readonly ITaskService _tasks;
        private readonly IEnvironmentService _environment;

        public Handler(ITaskService tasks, IEnvironmentService environment)
        {
            _tasks = tasks;
            _environment = environment;
        }

        public bool List { get; set; }
        public string? Task { get; set; }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var console = context.Console;

            if (!_environment.IsMsys2Installed())
            {
                console.Error.WriteLine("MSYS2 is not installed. Run 'm2m bootstrap' first.");
                return 1;
            }

            try
            {
                if (List)
                {
                    var availableTasks = await _tasks.GetTasksAsync(context.GetCancellationToken());

                    console.Out.WriteLine("Available tasks:");
                    foreach (var (name, task) in availableTasks)
                    {
                        var desc = task.Description ?? string.Empty;
                        console.Out.WriteLine($"  {name,-20} {desc}");
                    }

                    return 0;
                }

                if (string.IsNullOrWhiteSpace(Task))
                {
                    console.Error.WriteLine("Error: No task or command specified.");
                    return 1;
                }

                var tasks = await _tasks.GetTasksAsync(context.GetCancellationToken());

                if (tasks.ContainsKey(Task))
                {
                    return await _tasks.RunTaskAsync(Task!, context.GetCancellationToken());
                }
                else
                {
                    return await _tasks.RunCommandAsync(Task!, context.GetCancellationToken());
                }
            }
            catch (Exception ex)
            {
                console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }
    }
}
