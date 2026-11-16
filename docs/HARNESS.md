# Headless harness

Runs the real XIV Venue Manager plugin without the game, so the UI can be checked on a machine that cannot run FFXIV, or while plugins are down after a patch. Design: `docs/superpowers/specs/2026-11-16-headless-harness-design.md`.

It boots the real `Plugin` against fake Dalamud services, draws its windows through ImGui, and prints the screen as text. It cannot show anything that depends on real game state: housing detection, the object table, the real character, game icons and fonts. Check those in game.

## Requirements

- .NET 10 SDK.
- Dalamud's files, normally in `~/.xlcore/dalamud/Hooks/dev` (set `DALAMUD_HOME` to use another folder).
- `libcimgui.so` in `~/.aetherphone-harness/native`, built by Aetherphone's harness bootstrap (set `AETHERPHONE_HARNESS_CACHE` to use another cache). The build copies it next to the output as `cimgui.dll`.
- The harness also draws with Noto Sans CJK JP and FontAwesome from the same cache folder (`assets/UIRes`), and falls back to ImGui's default font when the files are missing.

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
| `wait [SECONDS]` | Sleeps real time (default 2, max 60), then draws two frames, so network data can arrive |
| `step [N]` | Draws N more frames |
| `shot PATH` | Writes a PNG screenshot |
| `link CODE` | Exchanges a dev pairing code for a session, saves it and links the plugin |
| `unlink` | Deletes the saved session and unlinks the plugin |
| `venues` | Lists the loaded venues as `<slug>  <name>`, or `(none loaded yet)` |
| `venue KEY` | Selects a venue by id, slug or unique name, like picking it in Settings |
| `state` | Prints the selected tab, the number of logged errors and warnings, `linked: yes\|no` and `link rejected: yes\|no` |
| `quit` | Stops |

Venues load in the background after linking, so run `wait` first. Names can repeat, so use the slug.

Do not `tap` buttons that open links ("Open xivvenuemanager.com"): the plugin opens them in your browser.

## Linking to the dev API

With no account linked, the main window always shows Settings. To see the other tabs, link the harness to your dev account with a pairing code:

1. Generate a code on the dev dashboard API Keys page: https://dev.xivvenuemanager.com/dashboard/api-keys
2. Run once with the code in the environment, or start the harness and type `link <code>`:

```bash
XVM_HARNESS_PAIRING_CODE=<code> dotnet run --project VenueManager.Harness
```

The session is stored in `~/.cache/xvm-harness/dev-session.json` (or under `$XDG_CACHE_HOME`), readable by your user only, and reused on the next start. `unlink` deletes it. If `state` shows `link rejected: yes` or the API answers 401, generate a new code and link again.

The harness only talks to `dev-api.xivvenuemanager.com`, with no override. Reads are harmless, but taps like Claim or Clock in write to your own dev data. The secret and the code are never printed. The plugin's own settings are never read or written: the harness uses a temporary folder. Use `wait` after `open` or `tab` to let the background fetches finish before `tree`.

Known limits: some saves and the doorbell fail on Linux and are logged (see `state`); two text items on one line read back as one.

## Run the tests

```bash
dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj
```

The harness projects are not in `VenueManager.sln`, so the release build and CI do not need these files.
