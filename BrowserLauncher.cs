using System.Diagnostics;

namespace SteamWebLauncher;

/// <summary>
/// Responsible ONLY for starting the browser process. Does not know or
/// care that the process it starts might just be a launcher stub — that's
/// WindowFinder's problem.
/// </summary>
internal static class BrowserLauncher
{
    public static Process Launch(BrowserType type, string url)
    {
        var definition = BrowserCatalog.Definitions[type];

        string? exePath = BrowserResolver.Find(type);
        if (exePath is null)
        {
            throw new FileNotFoundException(
                $"Could not locate {definition.ExeFileName} in the registry or any common install location.");
        }

        string profileDir = GetKioskProfileDirectory(type);
        Directory.CreateDirectory(profileDir);

        List<string> arguments = BuildArguments(definition, url, profileDir);
        Logger.Debug($"Launching: \"{exePath}\" {string.Join(' ', arguments)}");

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
        };

        // Use ArgumentList instead of a single Arguments string — it
        // handles quoting for paths with spaces (like the profile dir
        // under LocalAppData) correctly without us hand-rolling quotes.
        foreach (string arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        Process? process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException($"Process.Start returned null for {definition.ExeFileName}.");
        }

        return process;
    }

    private static List<string> BuildArguments(BrowserDefinition definition, string url, string profileDir)
    {
        // Both engines are single-instance by default: launching the exe
        // again while one is already running just forwards the URL to the
        // existing window as a new tab, and the new process exits
        // immediately, silently dropping kiosk mode in the process.
        // Pointing at a dedicated profile dir forces a genuinely separate
        // instance so kiosk mode actually takes effect, and as a bonus
        // keeps the kiosk session's profile separate from normal browsing.
        return definition.Engine switch
        {
            BrowserEngine.Chromium => BuildChromiumArguments(definition, url, profileDir),
            BrowserEngine.Gecko => BuildGeckoArguments(url, profileDir),
            _ => throw new NotSupportedException($"Unhandled browser engine: {definition.Engine}"),
        };
    }

    private static List<string> BuildChromiumArguments(BrowserDefinition definition, string url, string profileDir)
    {
        var args = new List<string>
        {
            $"--user-data-dir={profileDir}",
            "--kiosk",
            url,
        };

        // Edge specifically needs --edge-kiosk-type=fullscreen alongside
        // --kiosk, otherwise it can default kiosk mode to a locked-down
        // "public browsing" variant instead of plain fullscreen.
        if (definition.Type == BrowserType.Edge)
        {
            args.Add("--edge-kiosk-type=fullscreen");
        }

        return args;
    }

    private static List<string> BuildGeckoArguments(string url, string profileDir)
    {
        // Firefox uses single-dash flags and a different single-instance
        // mechanism than Chromium: -no-remote stops it from forwarding to
        // an already-running instance, and -profile points it at our
        // dedicated (auto-created on first run) kiosk profile dir.
        return new List<string>
        {
            "-no-remote",
            "-profile", profileDir,
            "-kiosk",
            url,
        };
    }

    private static string GetKioskProfileDirectory(BrowserType type)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "SteamWebLauncher", type.ToString(), "KioskProfile");
    }
}
