namespace Msys2Manager.Core.Configuration;

public class Msys2Configuration
{
    public string MSystem { get; set; } = "UCRT64";
    public string? Version { get; set; }  // MSYS2 version in YYYY-MM-DD format
    public string? BaseUrl { get; set; }  // Base directory URL for downloads
    public string? Mirror { get; set; }
    public bool AutoUpdate { get; set; } = true;
    public Dictionary<string, string> Packages { get; set; } = new();
    public Dictionary<string, TaskDefinition> Tasks { get; set; } = new();
}

public class TaskDefinition
{
    public string? Command { get; set; }
    public string[]? Commands { get; set; }
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
