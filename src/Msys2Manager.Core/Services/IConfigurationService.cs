using Msys2Manager.Core.Configuration;

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
        var parser = new Tomlet.TomlParser();
        return parser.Parse(content)!;
    }

    public async Task SaveConfigurationAsync(Msys2Configuration configuration, CancellationToken cancellationToken = default)
    {
        var configPath = _fileSystem.Path.Combine(_projectRoot, "msys2.toml");
        var parser = new Tomlet.TomlParser();
        var toml = parser.Serialize(configuration);
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
