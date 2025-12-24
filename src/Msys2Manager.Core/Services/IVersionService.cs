using System.Text.RegularExpressions;

namespace Msys2Manager.Core.Services;

public interface IVersionService
{
    Task<string[]> GetAvailableVersionsAsync(CancellationToken cancellationToken = default);
    Task<string> GetLatestVersionAsync(CancellationToken cancellationToken = default);
}

public class VersionService : IVersionService
{
    private static readonly HashSet<string> ValidVersions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly HttpClient _httpClient;

    public VersionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<string[]> GetAvailableVersionsAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (ValidVersions.Count > 0)
            {
                return ValidVersions.OrderDescending().ToArray();
            }

            var url = "https://repo.msys2.org/distrib/x86_64/";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            // Parse HTML to find directory links (version directories)
            // The HTML contains links like: <a href="2024-01-13/">2024-01-13/</a>
            var regex = new Regex(@"<a\s+href=""(\d{4}-\d{2}-\d{2})/""", RegexOptions.Compiled);
            var matches = regex.Matches(content);

            var versions = new List<string>();
            foreach (Match match in matches)
            {
                if (match.Success && match.Groups.Count > 1)
                {
                    versions.Add(match.Groups[1].Value);
                    ValidVersions.Add(match.Groups[1].Value);
                }
            }

            return versions.OrderDescending().ToArray();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string> GetLatestVersionAsync(CancellationToken cancellationToken = default)
    {
        var versions = await GetAvailableVersionsAsync(cancellationToken);
        return versions.FirstOrDefault() ?? "2024-01-13";
    }
}
