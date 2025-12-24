using Msys2Manager.Core.Configuration;
using Tomlyn;
using Tomlyn.Model;
using System.IO.Abstractions;

namespace Msys2Manager.Core.Services;

public interface IConfigurationService
{
    Task<Msys2Configuration> LoadConfigurationAsync(CancellationToken cancellationToken = default);
    Task SaveConfigurationAsync(Msys2Configuration configuration, CancellationToken cancellationToken = default);
    Task<Msys2LockFile> LoadLockFileAsync(CancellationToken cancellationToken = default);
    Task SaveLockFileAsync(Msys2LockFile lockFile, CancellationToken cancellationToken = default);
    string GetProjectRoot();
}

public class ConfigurationService : IConfigurationService
{
    private readonly IFileSystem _fileSystem;
    private readonly string _projectRoot;

    public ConfigurationService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        _projectRoot = FindProjectRoot();
    }

    public string GetProjectRoot() => _projectRoot;

    public async Task<Msys2Configuration> LoadConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var configPath = _fileSystem.Path.Combine(_projectRoot, "msys2.toml");

        if (!_fileSystem.File.Exists(configPath))
        {
            return new Msys2Configuration();
        }

        var content = await _fileSystem.File.ReadAllTextAsync(configPath, cancellationToken);
        var model = Toml.ToModel(content);

        var result = new Msys2Configuration();

        if (model.TryGetValue("msys2", out var msys2Table) && msys2Table is TomlTable msys2)
        {
            if (msys2.TryGetValue("msystem", out var msystem))
                result.MSystem = msystem.ToString() ?? "UCRT64";
            if (msys2.TryGetValue("version", out var version))
                result.Version = version.ToString();
            if (msys2.TryGetValue("base_url", out var baseUrl))
                result.BaseUrl = baseUrl.ToString();
            if (msys2.TryGetValue("mirror", out var mirror))
                result.Mirror = mirror.ToString();
            if (msys2.TryGetValue("auto_update", out var autoUpdate))
                result.AutoUpdate = bool.TryParse(autoUpdate.ToString(), out var au) && au;
        }

        if (model.TryGetValue("packages", out var packagesTable) && packagesTable is TomlTable packages)
        {
            foreach (var (key, value) in packages)
            {
                result.Packages[key] = value?.ToString() ?? "*";
            }
        }

        if (model.TryGetValue("tasks", out var tasksTable) && tasksTable is TomlTable tasks)
        {
            foreach (var (key, value) in tasks)
            {
                if (value is string str)
                {
                    result.Tasks[key] = new TaskDefinition { Command = str };
                }
                else if (value is TomlTable table)
                {
                    var task = new TaskDefinition();
                    if (table.TryGetValue("command", out var cmd))
                        task.Command = cmd.ToString();
                    if (table.TryGetValue("commands", out var cmds) && cmds is TomlArray arr)
                        task.Commands = arr.Select(x => x?.ToString()).ToArray()!;
                    if (table.TryGetValue("description", out var desc))
                        task.Description = desc.ToString();
                    result.Tasks[key] = task;
                }
            }
        }

        return result;
    }

    public async Task SaveConfigurationAsync(Msys2Configuration configuration, CancellationToken cancellationToken = default)
    {
        var configPath = _fileSystem.Path.Combine(_projectRoot, "msys2.toml");

        var model = new TomlTable
        {
            ["msys2"] = new TomlTable
            {
                ["msystem"] = configuration.MSystem,
                ["version"] = configuration.Version ?? string.Empty,
                ["base_url"] = configuration.BaseUrl ?? string.Empty,
                ["mirror"] = configuration.Mirror ?? string.Empty,
                ["auto_update"] = configuration.AutoUpdate
            },
            ["packages"] = new TomlTable()
        };

        foreach (var (key, value) in configuration.Packages)
        {
            ((TomlTable)model["packages"])[key] = value;
        }

        model["tasks"] = new TomlTable();
        foreach (var (key, value) in configuration.Tasks)
        {
            var taskTable = new TomlTable();
            if (value.Command is not null)
                taskTable["command"] = value.Command;
            if (value.Commands is not null)
            {
                var arr = new TomlArray();
                foreach (var cmd in value.Commands)
                    arr.Add(cmd);
                taskTable["commands"] = arr;
            }
            if (value.Description is not null)
                taskTable["description"] = value.Description;
            ((TomlTable)model["tasks"])[key] = taskTable;
        }

        var toml = Toml.FromModel(model);
        await _fileSystem.File.WriteAllTextAsync(configPath, toml, cancellationToken);
    }

    public async Task<Msys2LockFile> LoadLockFileAsync(CancellationToken cancellationToken = default)
    {
        var lockPath = _fileSystem.Path.Combine(_projectRoot, "msys2.lock");

        if (!_fileSystem.File.Exists(lockPath))
        {
            return new Msys2LockFile();
        }

        var content = await _fileSystem.File.ReadAllTextAsync(lockPath, cancellationToken);
        return Msys2LockFile.Parse(content);
    }

    public async Task SaveLockFileAsync(Msys2LockFile lockFile, CancellationToken cancellationToken = default)
    {
        var lockPath = _fileSystem.Path.Combine(_projectRoot, "msys2.lock");
        var content = lockFile.Serialize();
        await _fileSystem.File.WriteAllTextAsync(lockPath, content, cancellationToken);
    }

    private string FindProjectRoot()
    {
        var currentDir = _fileSystem.Directory.GetCurrentDirectory();

        while (currentDir is not null)
        {
            var tomlPath = _fileSystem.Path.Combine(currentDir, "msys2.toml");
            if (_fileSystem.File.Exists(tomlPath))
            {
                return currentDir;
            }

            var parent = _fileSystem.Directory.GetParent(currentDir);
            currentDir = parent?.FullName;
        }

        return _fileSystem.Directory.GetCurrentDirectory();
    }
}
