namespace SteamWebLauncher;

internal static class Program
{
    // How long we're willing to wait for the browser window to appear
    // before giving up and exiting (avoids hanging forever if the
    // browser fails to launch, e.g. bad URL or browser not installed).
    private static readonly TimeSpan WindowFindTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FindPollInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan WatchPollInterval = TimeSpan.FromMilliseconds(100);

    private static int Main(string[] args)
    {
        if (!TryParseArgs(args, out BrowserType browser, out string? url, out string? parseError))
        {
            Console.Error.WriteLine(parseError);
            PrintUsage();
            return 1;
        }

        Logger.Debug($"Browser: {browser}, URL: {url}");

        try
        {
            // 1. Snapshot every visible top-level HWND that exists right now.
            Logger.Debug("Capturing pre-launch window snapshot...");
            HashSet<IntPtr> preLaunchSnapshot = WindowSnapshot.Capture();
            Logger.Debug($"Found {preLaunchSnapshot.Count} existing window(s).");

            // 2. Launch the browser. For Edge/Chrome the returned process is
            //    just a launcher stub — it will exit almost immediately
            //    regardless of whether the real browser window opened.
            Logger.Debug($"Launching {browser}...");
            using var launchedProcess = BrowserLauncher.Launch(browser, url!);

            // 3. Poll until a new top-level HWND shows up that belongs to a
            //    Chromium process and wasn't in the snapshot.
            Logger.Debug("Searching for the new browser window...");
            IntPtr browserWindow = WindowFinder.FindNewBrowserWindow(
                preLaunchSnapshot, WindowFindTimeout, FindPollInterval);

            if (browserWindow == IntPtr.Zero)
            {
                Console.Error.WriteLine(
                    $"Timed out after {WindowFindTimeout.TotalSeconds}s waiting for the browser window to appear.");
                return 2;
            }

            // 4. Block here — this is the entire point of the app. Steam
            //    watches THIS process, so we stay alive exactly as long as
            //    the real browser window does.
            Logger.Debug("Watching window until it closes...");
            WindowWatcher.WaitForClose(browserWindow, WatchPollInterval);
            Logger.Debug("Window closed. Exiting.");

            // 5. Window destroyed -> fall through -> process exits -> Steam
            //    returns to Big Picture.
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SteamWebLauncher failed: {ex.Message}");
            return 1;
        }
    }

    private static bool TryParseArgs(
        string[] args, out BrowserType browser, out string? url, out string? error)
    {
        browser = BrowserType.Edge; // default
        url = null;
        error = null;

        foreach (string arg in args)
        {
            if (arg.StartsWith("--browser=", StringComparison.OrdinalIgnoreCase))
            {
                string value = arg["--browser=".Length..];
                if (!BrowserCatalog.TryParse(value, out browser))
                {
                    error = $"Unknown browser '{value}'. Supported: {BrowserCatalog.SupportedNamesForHelp()}.";
                    return false;
                }
            }
            else if (arg.Equals("--debug", StringComparison.OrdinalIgnoreCase))
            {
                Logger.DebugEnabled = true;
            }
            else if (url is null)
            {
                url = arg;
            }
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            error = "Missing URL.";
            return false;
        }

        return true;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: SteamWebLauncher.exe [--browser=<name>] [--debug] <url>");
        Console.Error.WriteLine($"  --browser=<name>   One of: {BrowserCatalog.SupportedNamesForHelp()} (default: edge)");
        Console.Error.WriteLine("  --debug            Print step-by-step diagnostic output");
        Console.Error.WriteLine("Example: SteamWebLauncher.exe --browser=brave https://youtube.com");
    }
}
