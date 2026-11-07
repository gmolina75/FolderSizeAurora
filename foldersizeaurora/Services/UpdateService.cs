using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FolderSizeAurora.Models;

namespace FolderSizeAurora.Services;

/// <summary>
/// Chequeo silencioso de actualizaciones contra GitHub Releases.
/// No bloquea la UI y solo informa si hay una versión más nueva.
/// </summary>
public sealed class UpdateService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private const string RepoOwner = "gmolina75";
    private const string RepoName = "FolderSizeAurora";
    private const string CurrentVersion = "1.0.0.0";

    private readonly Action<string>? _onUpdateAvailable;

    public UpdateService(Action<string>? onUpdateAvailable = null)
    {
        _onUpdateAvailable = onUpdateAvailable;
    }

    /// <summary>Ejecuta el chequeo en segundo plano. No lanza excepciones.</summary>
    public async Task CheckAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("FolderSizeAurora/1.0");

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return;

            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>();
            if (release is null || string.IsNullOrWhiteSpace(release.TagName)) return;

            var latestVersion = NormalizeVersion(release.TagName);
            var current = NormalizeVersion(CurrentVersion);

            if (latestVersion > current)
            {
                _onUpdateAvailable?.Invoke($"Nueva versión disponible: {release.TagName}. {release.HtmlUrl}");
            }
        }
        catch
        {
            // Silencioso: que no falle la app por un chequeo de actualización
        }
    }

    private static Version NormalizeVersion(string tag)
    {
        // Quita prefijo 'v' si existe: "v1.2.3" -> "1.2.3"
        var clean = tag.TrimStart('v', 'V');
        return Version.TryParse(clean, out var v) ? v : new Version(0, 0, 0, 0);
    }

    private sealed class GitHubRelease
    {
        public string TagName { get; set; } = "";
        public string HtmlUrl { get; set; } = "";
    }
}