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
                        Description = table.TryGetValue("description", out var desc) ? desc.ToString() : null
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

        var task = tasks[taskName];
        var commands = task.GetCommands();

        foreach (var command in commands)
        {
            var exitCode = await _environmentService.ExecuteCommandAsync(command, cancellationToken: cancellationToken);
            if (exitCode != 0)
            {
                return exitCode;
            }
        }

        return 0;
    }

    public async Task<int> RunCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        return await _environmentService.ExecuteCommandAsync(command, cancellationToken: cancellationToken);
    }
}
