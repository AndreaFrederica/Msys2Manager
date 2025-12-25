using Msys2Manager.Core.Configuration;
using Tomlyn;
using Tomlyn.Model;

namespace Msys2Manager.Core.Services;

public interface ITaskService
{
    Task<IReadOnlyDictionary<string, TaskDefinition>> GetTasksAsync(CancellationToken cancellationToken = default);
    Task<int> RunTaskAsync(string taskName, CancellationToken cancellationToken = default);
    Task<int> RunCommandAsync(string command, CancellationToken cancellationToken = default);
}

public class TaskService : ITaskService
{
    private readonly IConfigurationService _configurationService;
    private readonly IEnvironmentService _environmentService;

    public TaskService(IConfigurationService configurationService, IEnvironmentService environmentService)
    {
        _configurationService = configurationService;
        _environmentService = environmentService;
    }

    public async Task<IReadOnlyDictionary<string, TaskDefinition>> GetTasksAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configurationService.LoadConfigurationAsync(cancellationToken);

        var tasks = new Dictionary<string, TaskDefinition>();

        var configPath = _configurationService.GetProjectRoot();
        var tomlPath = System.IO.Path.Combine(configPath, "msys2.toml");

        if (!File.Exists(tomlPath))
            return tasks;

        var content = await File.ReadAllTextAsync(tomlPath, cancellationToken);
        var toml = Toml.ToModel(content);

        if (toml.TryGetValue("tasks", out var tasksTable) && tasksTable is TomlTable tasksTbl)
        {
            foreach (var (key, value) in tasksTbl)
            {
                if (value is TomlTable table)
                {
                    var task = new TaskDefinition
                    {
                        Command = table.TryGetValue("command", out var cmd) ? cmd.ToString() : null,
                        Commands = table.TryGetValue("commands", out var cmds) && cmds is TomlArray arr
                            ? arr.Select(x => x?.ToString()!).ToArray()
                            : null,
                        Description = table.TryGetValue("description", out var desc) ? desc.ToString() : null,
                        DependsOn = table.TryGetValue("depends_on", out var dep) && dep is TomlArray depArr
                            ? depArr.Select(x => x?.ToString()!).Where(x => x != null).ToArray()!
                            : null
                    };
                    tasks[key] = task;
                }
                else if (value is string str)
                {
                    tasks[key] = new TaskDefinition { Command = str };
                }
            }
        }

        return tasks;
    }

    public async Task<int> RunTaskAsync(string taskName, CancellationToken cancellationToken = default)
    {
        var tasks = await GetTasksAsync(cancellationToken);

        if (!tasks.ContainsKey(taskName))
        {
            Console.WriteLine($"Task '{taskName}' not found.");
            return 1;
        }

        // Resolve dependencies in topological order
        var executionOrder = ResolveDependencies(taskName, tasks);
        if (executionOrder is null)
        {
            Console.WriteLine($"Error: Circular dependency detected involving task '{taskName}'.");
            return 1;
        }

        // Run tasks in dependency order
        foreach (var taskToRun in executionOrder)
        {
            if (!tasks.ContainsKey(taskToRun))
            {
                Console.WriteLine($"Dependency task '{taskToRun}' not found.");
                return 1;
            }

            var task = tasks[taskToRun];
            var commands = task.GetCommands();

            foreach (var command in commands)
            {
                var exitCode = await _environmentService.ExecuteCommandAsync(command, cancellationToken: cancellationToken);
                if (exitCode != 0)
                {
                    return exitCode;
                }
            }
        }

        return 0;
    }

    private static List<string>? ResolveDependencies(string taskName, IReadOnlyDictionary<string, TaskDefinition> tasks)
    {
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();
        var result = new List<string>();

        if (!Visit(taskName, tasks, visited, visiting, result))
        {
            return null; // Cycle detected
        }

        return result;
    }

    private static bool Visit(string taskName, IReadOnlyDictionary<string, TaskDefinition> tasks, HashSet<string> visited, HashSet<string> visiting, List<string> result)
    {
        if (visited.Contains(taskName))
        {
            return true;
        }

        if (visiting.Contains(taskName))
        {
            // Cycle detected
            return false;
        }

        visiting.Add(taskName);

        if (tasks.TryGetValue(taskName, out var task) && task.DependsOn is not null)
        {
            foreach (var dep in task.DependsOn)
            {
                if (!Visit(dep, tasks, visited, visiting, result))
                {
                    return false;
                }
            }
        }

        visiting.Remove(taskName);
        visited.Add(taskName);
        result.Add(taskName);

        return true;
    }

    public async Task<int> RunCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        return await _environmentService.ExecuteCommandAsync(command, cancellationToken: cancellationToken);
    }
}
