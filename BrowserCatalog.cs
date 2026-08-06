namespace SteamWebLauncher;

internal enum BrowserType
{
    Edge,
    Chrome,
    Brave,
    Opera,
    Vivaldi,
    Firefox,
}

internal enum BrowserEngine
{
    Chromium,
    Gecko,
}

/// <summary>
/// Static metadata about each supported browser: what its process is
/// called, what its exe file is called, where it typically installs, and
/// which engine it uses (so BrowserLauncher knows which command-line
/// flag scheme applies). This is the single place to touch when adding a
/// new browser — nothing in BrowserLauncher, BrowserResolver, or
/// WindowFinder needs structural changes, just a new dictionary entry.
/// </summary>
internal sealed record BrowserDefinition(
    BrowserType Type,
    BrowserEngine Engine,
    string ProcessName,
    string ExeFileName,
    // Subpaths are tried under ProgramFiles, ProgramFilesX86, and
    // LocalApplicationData in that order (most Chromium browsers other
    // than Edge/Chrome install per-user under LocalAppData by default).
    string[] InstallSubPath);

internal static class BrowserCatalog
{
    public static readonly IReadOnlyDictionary<BrowserType, BrowserDefinition> Definitions =
        new Dictionary<BrowserType, BrowserDefinition>
        {
            [BrowserType.Edge] = new(
                BrowserType.Edge, BrowserEngine.Chromium, "msedge", "msedge.exe",
                new[] { "Microsoft", "Edge", "Application" }),

            [BrowserType.Chrome] = new(
                BrowserType.Chrome, BrowserEngine.Chromium, "chrome", "chrome.exe",
                new[] { "Google", "Chrome", "Application" }),

            [BrowserType.Brave] = new(
                BrowserType.Brave, BrowserEngine.Chromium, "brave", "brave.exe",
                new[] { "BraveSoftware", "Brave-Browser", "Application" }),

            [BrowserType.Opera] = new(
                BrowserType.Opera, BrowserEngine.Chromium, "opera", "opera.exe",
                new[] { "Opera" }),

            [BrowserType.Vivaldi] = new(
                BrowserType.Vivaldi, BrowserEngine.Chromium, "vivaldi", "vivaldi.exe",
                new[] { "Vivaldi", "Application" }),

            [BrowserType.Firefox] = new(
                BrowserType.Firefox, BrowserEngine.Gecko, "firefox", "firefox.exe",
                new[] { "Mozilla Firefox" }),
        };

    public static bool TryParse(string value, out BrowserType type) =>
        Enum.TryParse(value, ignoreCase: true, out type) && Definitions.ContainsKey(type);

    public static string SupportedNamesForHelp() =>
        string.Join(", ", Definitions.Keys.Select(k => k.ToString().ToLowerInvariant()));
}
