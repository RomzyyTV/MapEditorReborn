using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Exiled.API.Features;

namespace MapEditorReborn.API.Extensions;

public static class Update
{
    private static readonly string RepositoryUrl = "https://api.github.com/repos/RomzyyTV/MapEditorReborn/releases/latest";
    private static readonly string PluginPath = Path.Combine(Paths.Plugins, "MapEditorReborn.dll");
    private static readonly string CurrentVersion = MapEditorReborn.Singleton.Version.ToString();
    private static readonly HttpClient HttpClient = new HttpClient
    {
        DefaultRequestHeaders = { { "User-Agent", "UpdateChecker" } }
    };
    
    public static void RegisterEvents()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers += WaitingForPlayers;
    }
    public static void UnregisterEvents()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers -= WaitingForPlayers;
    }

    private static void WaitingForPlayers()
    {
        Log.Info("Checking for updates...");
        Task.Run(() => CheckForUpdates(true));
    }
    private static async Task CheckForUpdates(bool autoUpdate)
    {
        try
        {
            var response = await HttpClient.GetAsync(RepositoryUrl);
            if (!response.IsSuccessStatusCode)
            {
                Log.Error($"Failed to check for updates: {response.StatusCode} - {response.ReasonPhrase}");
                return;
            }

            var content = await response.Content.ReadAsStringAsync();
            var latestVersion = ExtractLatestVersion(content);
            var downloadUrl = ExtractDownloadUrl(content);

            if (latestVersion == null || downloadUrl == null)
            {
                Log.Error("Failed to parse update information.");
                return;
            }

            if (IsNewerVersion(CurrentVersion, latestVersion))
            {
                Log.Warn($"A new version is available: {latestVersion} (current: {CurrentVersion})");

                if (autoUpdate)
                {
                    Log.Info("Automatic update is enabled. Downloading and applying the update...");
                    await UpdatePluginAsync(downloadUrl);
                    RestartRound();
                }
                else
                {
                    Log.Warn("Automatic update is disabled. Please download the update manually.");
                }
            }
            else
            {
                Log.Info("You are using the latest version.");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error while checking for updates: {ex.Message}");
        }
    }
    private static void RestartRound()
    {
        try
        {
            // Restart Command
            string command = "rnr";
            Server.ExecuteCommand(command);
            Log.Info("Round restart initiated.");
        }
        catch (Exception ex)
        {
            Log.Error($"Error while restarting the round: {ex.Message}");
        }
    }
    private static string ExtractLatestVersion(string json)
    {
        try
        {
            var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
            return obj["tag_name"]?.ToString();
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to extract the latest version: {ex.Message}");
            return null;
        }
    }
    private static string ExtractDownloadUrl(string json)
    {
        try
        {
            var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
            return obj["assets"]?[0]?["browser_download_url"]?.ToString();
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to extract download URL: {ex.Message}");
            return null;
        }
    }
    private static bool IsNewerVersion(string currentVersion, string latestVersion)
    {
        if (Version.TryParse(currentVersion, out var current) && Version.TryParse(latestVersion, out var latest))
        {
            return latest > current;
        }

        Log.Warn("Failed to compare versions. Using current version as the latest.");
        return false;
    }
    private static async Task UpdatePluginAsync(string downloadUrl)
    {
        try
        {
            var pluginData = await HttpClient.GetByteArrayAsync(downloadUrl);
            BackupAndWritePlugin(pluginData);
            Log.Info("Plugin updated successfully. Restart the server to apply changes.");
        }
        catch (Exception ex)
        {
            Log.Error($"Error during plugin update: {ex.Message}");
        }
    }
    private static void BackupAndWritePlugin(byte[] pluginData)
    {
        if (MapEditorReborn.Singleton.Config.EnableBackup)
        {
            string backupPath = PluginPath + ".backup";
            if (File.Exists(PluginPath))
            {
                File.Copy(PluginPath, backupPath, overwrite: true);
                Log.Warn($"Backup created: {backupPath}");
            }
        }

        File.WriteAllBytes(PluginPath, pluginData);
    }
}