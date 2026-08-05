namespace SteamWebLauncher;

/// <summary>
/// Keeps SteamWebLauncher.exe alive for as long as the tracked browser
/// window exists. This is what Steam is actually watching — as soon as
/// this returns, the launcher exits and Steam drops out of "STOP" state
/// back to Big Picture.
/// </summary>
internal static class WindowWatcher
{
    public static void WaitForClose(IntPtr hWnd, TimeSpan pollInterval)
    {
        while (NativeMethods.IsWindow(hWnd))
        {
            Thread.Sleep((int)pollInterval.TotalMilliseconds);
        }
    }
}
