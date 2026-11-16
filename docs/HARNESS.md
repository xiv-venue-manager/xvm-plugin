# Headless harness

Runs the real XIV Venue Manager plugin without the game, so the UI can be checked on a machine that cannot run FFXIV, or while plugins are down after a patch. Design: `docs/superpowers/specs/2026-11-16-headless-harness-design.md`.

It boots the real `Plugin` against fake Dalamud services, draws its windows through ImGui, and prints the screen as text. It cannot show anything that depends on real game state: housing detection, the object table, the real character, game icons and fonts. Check those in game.

## Requirements

- .NET 10 SDK.
- Dalamud's files, normally in `~/.xlcore/dalamud/Hooks/dev` (set `DALAMUD_HOME` to use another folder).
- `libcimgui.so` in `~/.aetherphone-harness/native`, built by Aetherphone's harness bootstrap (set `AETHERPHONE_HARNESS_CACHE` to use another cache). The build copies it next to the output as `cimgui.dll`.

If the installed Dalamud ImGui bindings differ from the harness cache, the command line stops with a message. Re-run Aetherphone's harness bootstrap.

## Use it

One command per line on standard input, plain text on standard output:

```bash
printf 'open Main\ntabs\ntree\nquit\n' | dotnet run --project VenueManager.Harness
```

| Command | What it does |
| --- | --- |
| `open [WINDOW]` | Opens the window whose name contains WINDOW (default `Main`) and draws it |
| `tabs` | Lists the plugin's tabs |
| `tab NAME` | Selects a tab (only sticks when an account is linked, see below) |
| `tree` | Prints every visible text item as `(x,y) text` |
| `tap TEXT` | Clicks the item with exactly that label |
| `command TEXT` | Runs a registered chat command, for example `command /xvm snooze` |
| `step [N]` | Draws N more frames |
| `shot PATH` | Writes a PNG screenshot |
| `state` | Prints the selected tab and the number of logged errors and warnings |
| `quit` | Stops |

Do not `tap` buttons that open links ("Open xivvenuemanager.com"): the plugin opens them in your browser.

## Linked account

With no account linked, the main window always shows Settings. To see the other tabs, give the harness an API to talk to:

```bash
XVM_HARNESS_URL=http://127.0.0.1:8000 XVM_HARNESS_SECRET=<pairing secret> \
  dotnet run --project VenueManager.Harness
```

A secret needs an explicit URL, so the harness never contacts the production API by accident. Use a local or dev xvm-api. The plugin's own settings and venue data are never read or written: the harness uses a temporary folder.

Known limits: sidebar icons read back as `?` (use `tabs` and `tab` instead); some saves and the doorbell fail on Linux and are logged (see `state`); two text items on one line read back as one.

## Run the tests

```bash
dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj
```

The harness projects are not in `VenueManager.sln`, so the release build and CI do not need these files.
