# SteamWebLauncher

Lets Steam Big Picture launch and correctly track Chromium browser apps
(Edge, Chrome, Brave, etc.) as if they were games.

## Just want to use it? Start here.

No coding, no building, no installing .NET — just download and point Steam
at it.

1. Go to the **[Releases](../../releases)** page (right sidebar of the repo,
   or the link above) and download the latest `SteamWebLauncher-*.zip`.
2. Right-click the zip → **Extract All** → pick a folder you'll keep it in
   (don't put it somewhere temporary — Steam needs to keep finding it).
3. In Steam, go to your Library and click **Add a Game → Add a Non-Steam
   Game** (or the **+** button, depending on your Steam version).
4. Click **Browse**, find the extracted `SteamWebLauncher.exe`, select it,
   then click **Add Selected Programs**.
5. Find it in your library, right-click it → **Properties**.
6. In the **Launch Options** box, type the site you want it to open, for
   example:
   ```
   https://youtube.com
   ```
   Want a browser other than the default (Edge)? Put `--browser=` in front,
   for example:
   ```
   --browser=brave https://youtube.com
   ```
   Supported browsers: `edge`, `chrome`, `brave`, `opera`, `vivaldi`, `firefox`.
7. Close Properties. Optionally rename the shortcut and set custom
   artwork/icon like you would for any other game.
8. Launch it from your Library or Big Picture — it'll open the site
   fullscreen, and closing the browser (Alt+F4, or Steam's own **Exit
   Game** from the Big Picture overlay) returns you straight to Steam,
   exactly like closing a game.

That's it. Everything below this point is for people who want to look at
or modify the source code.

---

## Why

Steam only watches whether the process it launched is still alive. Edge's
`msedge.exe --app=<url>` invocation spawns the real browser window from a
launcher stub that exits almost immediately — so Steam thinks the "game"
closed instantly, even though the browser window is still open.

SteamWebLauncher works around this by tracking the actual browser **window**
(HWND) instead of the launcher process: it snapshots existing windows,
launches the browser, finds the new window that appears, and blocks until
that window is destroyed. Steam watches SteamWebLauncher.exe the whole time.

## Build

Requires the .NET 8 SDK on Windows (this is a Windows-only app —
`net8.0-windows`, uses `user32.dll`).

```
dotnet build -c Release
```

Output: `bin\Release\net8.0-windows\SteamWebLauncher.exe`

## Use in Steam

1. Add a Non-Steam Game.
2. Program: path to `SteamWebLauncher.exe`.
3. Launch Options: `[--browser=<name>] <url>`, e.g. `https://youtube.com` or
   `--browser=brave https://youtube.com`.

Steam will behave as if it launched a normal game — it waits while the
browser window is open, and returns to Big Picture the moment you close it
(Alt+F4, or Steam's own "Exit Game" from the Big Picture overlay).

## Usage

```
SteamWebLauncher.exe [--browser=<name>] [--debug] <url>
```

- `--browser=<name>` — one of `edge`, `chrome`, `brave`, `opera`, `vivaldi`.
  Defaults to `edge` if omitted.
- `--debug` — prints step-by-step diagnostic output (snapshot size, launch
  command, resolved exe path, matched HWND, etc.) to the console.

Examples:
```
SteamWebLauncher.exe https://youtube.com
SteamWebLauncher.exe --browser=brave https://youtube.com
SteamWebLauncher.exe --browser=chrome --debug https://miruro.tv
```

Browsers launch in `--kiosk` (true fullscreen, no window chrome) using a
dedicated profile under `%LocalAppData%\SteamWebLauncher\<Browser>\KioskProfile`
— separate from your normal browsing profile. Exit with Alt+F4 or Steam's
Big Picture "Exit Game."

Note: the first launch for a given browser will show that browser's normal
first-run/welcome screen inside the kiosk window, since the profile is
brand new. It won't happen again after that — the profile persists.

## Status

Feature-complete and in daily use. Supports Edge, Chrome, Brave, Opera,
Vivaldi (Chromium engine), and Firefox (Gecko engine); auto-detects
install paths; launches fullscreen kiosk; correctly tracks the browser
window through Steam even when the browser was already running.

Possible future ideas, not currently planned:
- Launch installed PWAs directly via `--app-id=`
- A config file mapping short names (`youtube`) to URLs, so Steam launch
  options don't need full URLs
- Packaged installer / GitHub Release with a prebuilt exe

## Design notes

- No timers as a substitute for the real signal — we wait on the actual
  HWND lifecycle (`IsWindow`), not a fixed delay.
- No window title matching — title strings change (tab title, mid-load
  state, etc.) and are a fragile thing to key off of.
- No guessing based on process count — `Process.MainWindowHandle` doesn't
  work here because `Process.Start()` returns a launcher stub for some
  browsers, not the process that owns the real window.
- Launches with a dedicated profile dir per browser (via `--user-data-dir`
  for Chromium, `-profile` for Firefox) rather than the browser's default
  profile. Both engines are single-instance by default — launching the exe
  again while one is already running just forwards the URL to the existing
  window as a new tab, and the new process exits immediately, silently
  dropping kiosk mode in the process. A separate profile dir forces a
  genuinely new instance so kiosk mode actually applies, regardless of
  whether the browser was already open.
- Browser-specific knowledge is split by *engine*, not by individual
  browser: `BrowserCatalog.cs` tags each browser as `Chromium` or `Gecko`,
  and `BrowserLauncher.BuildArguments` has one flag-building method per
  engine (`--kiosk`/`--user-data-dir` for Chromium, `-kiosk`/`-profile`/
  `-no-remote` for Firefox's Gecko engine), plus the one Edge-specific
  `--edge-kiosk-type=fullscreen` exception. `WindowFinder` and
  `BrowserResolver` don't care about engine at all — they just work off
  whichever `BrowserType` was actually launched. Adding a browser on an
  already-supported engine (e.g. a Chromium fork) is purely a
  `BrowserCatalog` entry; adding a genuinely new engine means one new case
  in `BuildArguments`.
- Window candidate scoring (`WindowFinder.Score`) is a heuristic, not a
  guarantee: foreground-window status and window size, weighted so a
  fullscreen kiosk window reliably outscores a small dialog or toast. It's
  meant to be meaningfully better than "first HWND enumerated", not
  bulletproof — a scenario with two simultaneous full-screen windows from
  the same browser would still be ambiguous.

## Known limitations

- `WindowFinder`'s 300ms "settle" pause before scoring is a fixed delay,
  not adaptive — a very slow-to-appear secondary dialog could still be
  missed on that pass (though it would just get picked up as a separate,
  ignorable window later, not break tracking of the main one).
- Registry-based resolution checks the standard "App Paths" key; some
  portable or heavily sandboxed installs may not register there and will
  fall through to the hardcoded common paths, which can still miss a
  nonstandard install location.
- Opera/Vivaldi/Firefox install-path detection is less battle-tested than
  Edge/Chrome/Brave since I don't have those installed to verify against —
  if resolution fails for one, `--debug` will show exactly which paths
  were checked.
- Firefox's `-profile` flag creates the kiosk profile silently on first
  run in normal cases, but if Firefox is already running under a
  *different* profile when SteamWebLauncher launches it, Firefox can
  occasionally show a "choose profile" prompt instead of proceeding
  straight to kiosk mode. Closing all Firefox windows before first launch
  avoids it; subsequent launches (once the kiosk profile already exists)
  haven't shown this.
