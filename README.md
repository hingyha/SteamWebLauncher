# SteamWebLauncher

Lets Steam Big Picture launch and correctly track Chromium browser apps
(Edge, Chrome, Brave, etc.) as if they were games.

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

Browsers launch in `--kiosk` (true fullscreen, no window chrome). Exit with
Alt+F4 or Steam's Big Picture "Exit Game."

## Status: v2

- [x] Edge, Chrome, Brave, Opera, Vivaldi support via a `BrowserType` +
      `BrowserDefinition` abstraction (`BrowserCatalog.cs`) — adding a new
      browser is one dictionary entry, no changes elsewhere
- [x] `--browser=<name>` command-line selection (default: Edge)
- [x] Install-path auto-detection via the Windows "App Paths" registry key,
      falling back to common Program Files / LocalAppData locations
      (`BrowserResolver.cs`)
- [x] Multi-candidate window scoring in `WindowFinder` — if more than one
      new Chromium window appears at once (update dialog, session-restore
      prompt, notification toast), picks the best match instead of
      whichever HWND `EnumWindows` happened to report first
- [x] `--debug` flag for step-by-step diagnostic logging (`Logger.cs`)
- [x] Fullscreen kiosk launch (`--kiosk`, plus `--edge-kiosk-type=fullscreen`
      for Edge specifically)
- [ ] v3: Launch installed PWAs via `--app-id=`
- [ ] v4: Config file mapping short names (`youtube`) to URLs
- [ ] v5: Installer, icon, GitHub release, README, MIT license

## Design notes

- No timers as a substitute for the real signal — we wait on the actual
  HWND lifecycle (`IsWindow`), not a fixed delay.
- No window title matching — title strings change (tab title, mid-load
  state, etc.) and are a fragile thing to key off of.
- No guessing based on process count — `Process.MainWindowHandle` doesn't
  work here because `Process.Start()` returns a launcher stub for some
  browsers, not the process that owns the real window.
- Browser-specific knowledge lives in exactly two places:
  `BrowserCatalog.cs` (what a browser is called and where it installs) and
  the one `type == BrowserType.Edge` branch in `BrowserLauncher.BuildArguments`
  (Edge's kiosk-type quirk). Everything else — `WindowFinder`,
  `BrowserResolver`, `Program` — is already browser-agnostic.
- Window candidate scoring (`WindowFinder.Score`) is a heuristic, not a
  guarantee: foreground-window status and window size, weighted so a
  fullscreen kiosk window reliably outscores a small dialog or toast. It's
  meant to be meaningfully better than "first HWND enumerated", not
  bulletproof — a scenario with two simultaneous full-screen Chromium
  windows would still be ambiguous.

## Known limitations (v2)

- `WindowFinder`'s 300ms "settle" pause before scoring is a fixed delay,
  not adaptive — a very slow-to-appear secondary dialog could still be
  missed on that pass (though it would just get picked up as a separate,
  ignorable window later, not break tracking of the main one).
- Registry-based resolution checks the standard "App Paths" key; some
  portable or heavily sandboxed installs may not register there and will
  fall through to the hardcoded common paths, which can still miss a
  nonstandard install location.
- Opera/Vivaldi install-path detection is less battle-tested than
  Edge/Chrome/Brave since I don't have those installed to verify against —
  if resolution fails for one, `--debug` will show exactly which paths
  were checked.
