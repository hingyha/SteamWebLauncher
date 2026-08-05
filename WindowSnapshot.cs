namespace SteamWebLauncher;

/// <summary>
/// Captures the set of visible top-level HWNDs at a point in time.
/// Called once, right before we launch the browser, so WindowFinder
/// has something to diff against.
/// </summary>
internal static class WindowSnapshot
{
    public static HashSet<IntPtr> Capture()
    {
        var handles = new HashSet<IntPtr>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (NativeMethods.IsWindowVisible(hWnd))
            {
                handles.Add(hWnd);
            }
            return true; // keep enumerating
        }, IntPtr.Zero);

        return handles;
    }
}
