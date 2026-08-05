using Microsoft.Win32;

namespace SteamWebLauncher;

/// <summary>
/// Finds the installed exe for a given browser. Checks the Windows
/// "App Paths" registry key first (the same mechanism Explorer uses to
/// resolve "chrome.exe" typed into Run), then falls back to checking
/// common install locations directly, since App Paths entries aren't
/// guaranteed to exist for every browser/install method (e.g. some
/// portable or MSIX installs skip it).
/// </summary>
internal static class BrowserResolver
{
    private const string AppPathsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\";

    public static string? Find(BrowserType type)
    {
        var definition = BrowserCatalog.Definitions[type];

        string? fromRegistry = FindViaAppPaths(definition.ExeFileName);
        if (fromRegistry is not null)
        {
            Logger.Debug($"Resolved {definition.ExeFileName} via App Paths registry: {fromRegistry}");
            return fromRegistry;
        }

        string? fromDisk = FindViaCommonInstallLocations(definition);
        if (fromDisk is not null)
        {
            Logger.Debug($"Resolved {definition.ExeFileName} via common install path: {fromDisk}");
            return fromDisk;
        }

        Logger.Debug($"Could not resolve an install path for {definition.ExeFileName}.");
        return null;
    }

    private static string? FindViaAppPaths(string exeFileName)
    {
        // App Paths entries can live under either hive depending on
        // whether the browser installed machine-wide or per-user.
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var key = hive.OpenSubKey(AppPathsKey + exeFileName);
                if (key?.GetValue(null) is string path && File.Exists(path))
                {
                    return path;
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
            {
                // No access to this hive/key — fall through and try the next one.
            }
        }

        return null;
    }

    private static string? FindViaCommonInstallLocations(BrowserDefinition definition)
    {
        string[] baseFolders =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };

        foreach (var baseFolder in baseFolders)
        {
            if (string.IsNullOrEmpty(baseFolder)) continue;

            string candidate = Path.Combine(
                new[] { baseFolder }.Concat(definition.InstallSubPath).Append(definition.ExeFileName).ToArray());

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
