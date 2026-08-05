namespace SteamWebLauncher;

/// <summary>
/// Trivial gated logger. Program.cs flips DebugEnabled based on the
/// --debug flag before doing anything else; everything else just calls
/// Logger.Debug(...) without caring whether it's on.
/// </summary>
internal static class Logger
{
    public static bool DebugEnabled { get; set; }

    public static void Debug(string message)
    {
        if (DebugEnabled)
        {
            Console.WriteLine($"[debug] {message}");
        }
    }
}
