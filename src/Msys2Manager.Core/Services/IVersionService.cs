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

            // Parse HTML to find msys2-base-x86_64-<version>.tar.zst files
            // Format: msys2-base-x86_64-YYYYMMDD.tar.zst
            // Extract YYYYMMDD and convert to YYYY-MM-DD format
            var regex = new Regex(@"msys2-base-x86_64-(\d{4})(\d{2})(\d{2})\.tar\.", RegexOptions.Compiled);
            var matches = regex.Matches(content);

            var versions = new List<string>();
            foreach (Match match in matches)
            {
                if (match.Success && match.Groups.Count > 3)
                {
                    var year = match.Groups[1].Value;
                    var month = match.Groups[2].Value;
                    var day = match.Groups[3].Value;
                    var version = $"{year}-{month}-{day}";
                    if (ValidVersions.Add(version))
                    {
                        versions.Add(version);
                    }
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
        return versions.FirstOrDefault() ?? "2025-12-13";
    }
}
