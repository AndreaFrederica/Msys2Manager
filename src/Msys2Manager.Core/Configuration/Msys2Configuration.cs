using Tomlet.Attributes;

namespace Msys2Manager.Core.Configuration;

public class Msys2Configuration
{
    [TomlProperty("msystem")]
    public string MSystem { get; set; } = "UCRT64";

    [TomlProperty("base_url")]
    public string? BaseUrl { get; set; }

    [TomlProperty("mirror")]
    public string? Mirror { get; set; }

    [TomlProperty("auto_update")]
    public bool AutoUpdate { get; set; } = true;

    [TomlProperty("packages")]
    public PackagesSection Packages { get; set; } = new();
}

public class PackagesSection
{
    [TomlPreferTextMode]
    public Dictionary<string, string> Items { get; set; } = new();

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => Items.GetEnumerator();

    public void Add(string key, string value) => Items.Add(key, value);

    public bool ContainsKey(string key) => Items.ContainsKey(key);

    public bool Remove(string key) => Items.Remove(key);

    public int Count => Items.Count;
}

public class TasksSection
{
    [TomlPreferTextMode]
    public Dictionary<string, TaskDefinition> Items { get; set; } = new();

    public IEnumerator<KeyValuePair<string, TaskDefinition>> GetEnumerator() => Items.GetEnumerator();

    public void Add(string key, TaskDefinition value) => Items.Add(key, value);

    public bool ContainsKey(string key) => Items.ContainsKey(key);

    public int Count => Items.Count;
}

[TomlDoNotInlineMetadata]
public class TaskDefinition
{
    [TomlProperty("command")]
    public string? Command { get; set; }

    [TomlProperty("commands")]
    public string[]? Commands { get; set; }

    [TomlProperty("description")]
    public string? Description { get; set; }

    public string[] GetCommands()
    {
        if (Commands is not null && Commands.Length > 0)
            return Commands;

        if (Command is not null)
            return new[] { Command };

        return Array.Empty<string>();
    }
}
