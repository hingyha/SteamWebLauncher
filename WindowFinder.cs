using System.Diagnostics;

namespace SteamWebLauncher;

internal static class WindowFinder
{
    // Chromium browser exe names we know how to recognize. Kept as a
    // flat set (rather than pulled from BrowserCatalog) since we want to
    // recognize *any* supported browser's window here, regardless of
    // which one was actually launched — a stray window from a different
    // already-running Chromium browser should still be correctly ignored
    // by the pre-launch snapshot diff, but if it somehow shows up new,
    // treating it as a candidate and letting scoring sort it out is safer
    // than silently missing the real match.
    private static readonly HashSet<string> ChromiumProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "msedge",
        "chrome",
        "brave",
        "opera",
        "vivaldi",
    };

    /// <summary>
    /// Polls until at least one new top-level HWND appears that matches
    /// the baseline filters (not in the pre-launch snapshot, visible,
    /// true top-level window, non-empty title, belongs to a known
    /// Chromium process). If multiple candidates show up around the same
    /// time — an update dialog, a "restore session?" prompt, a
    /// notification toast, alongside the real app window — scores them
    /// and returns the best match instead of just the first one seen.
    /// Returns IntPtr.Zero on timeout.
    /// </summary>
    public static IntPtr FindNewBrowserWindow(
        HashSet<IntPtr> preLaunchSnapshot,
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var candidates = CollectCandidates(preLaunchSnapshot);

            if (candidates.Count > 0)
            {
                Logger.Debug($"{candidates.Count} candidate window(s) found, settling briefly for any dialogs/popups...");

                // Give any secondary windows (update dialogs, session
                // restore prompts, notification popups) a moment to show
                // up alongside the real app window, so scoring has the
                // full picture instead of racing whichever HWND enumerated
                // first.
                Thread.Sleep(300);
                candidates = CollectCandidates(preLaunchSnapshot);

                IntPtr best = PickBest(candidates);
                Logger.Debug($"Selected HWND 0x{best:X} as the app window (title: \"{NativeMethods.GetWindowTitle(best)}\").");
                return best;
            }

            Thread.Sleep((int)pollInterval.TotalMilliseconds);
        }

        return IntPtr.Zero;
    }

    private static List<IntPtr> CollectCandidates(HashSet<IntPtr> preLaunchSnapshot)
    {
        var candidates = new List<IntPtr>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (preLaunchSnapshot.Contains(hWnd)) return true;
            if (!NativeMethods.IsWindowVisible(hWnd)) return true;

            // Only consider true top-level windows, not owned popups.
            if (NativeMethods.GetAncestor(hWnd, NativeMethods.GA_ROOT) != hWnd) return true;

            if (string.IsNullOrWhiteSpace(NativeMethods.GetWindowTitle(hWnd))) return true;
            if (!BelongsToChromium(hWnd)) return true;

            candidates.Add(hWnd);
            return true; // keep enumerating so we catch every candidate, not just the first
        }, IntPtr.Zero);

        return candidates;
    }

    private static IntPtr PickBest(List<IntPtr> candidates)
    {
        if (candidates.Count == 1) return candidates[0];

        IntPtr foreground = NativeMethods.GetForegroundWindow();

        return candidates
            .Select(hWnd => (hWnd, score: Score(hWnd, foreground)))
            .OrderByDescending(c => c.score)
            .First()
            .hWnd;
    }

    /// <summary>
    /// Heuristic score used only to break ties when multiple new Chromium
    /// windows appear at once. Two signals: is it the foreground window
    /// (a real app window that just launched almost always is; a
    /// background update-check dialog usually isn't), and how large is it
    /// (the real --kiosk/--app window is fullscreen or large; dialogs and
    /// toasts are small). Not meant to be bulletproof — just meaningfully
    /// better than "first HWND EnumWindows happens to report".
    /// </summary>
    private static int Score(IntPtr hWnd, IntPtr foreground)
    {
        int score = 0;

        if (hWnd == foreground)
        {
            score += 50;
        }

        if (NativeMethods.GetWindowRect(hWnd, out var rect))
        {
            long width = Math.Max(0, rect.Right - rect.Left);
            long height = Math.Max(0, rect.Bottom - rect.Top);
            long area = width * height;

            // Scale down into a comparable range; exact units don't matter,
            // only the relative ordering between candidates does.
            score += (int)Math.Min(area / 5_000, 100);
        }

        return score;
    }

    private static bool BelongsToChromium(IntPtr hWnd)
    {
        try
        {
            uint pid = NativeMethods.GetProcessIdForWindow(hWnd);
            if (pid == 0) return false;

            using var process = Process.GetProcessById((int)pid);
            return ChromiumProcessNames.Contains(process.ProcessName);
        }
        catch (ArgumentException)
        {
            // Process exited between EnumWindows and GetProcessById — treat as no match.
            return false;
        }
    }
}
