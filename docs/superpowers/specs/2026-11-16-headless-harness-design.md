# Headless harness and testable UI logic

## Goal

Let a person or a Claude session see and drive the real plugin UI without launching FFXIV, and keep working when a game patch has broken Dalamud plugins. Today only pure helpers have unit tests. Anything inside an `ImGui.*` draw call is checked in game only.

## Non-goals

- Anything that depends on real game state: housing and territory detection, the object table, the real character, `AgentMap`. These are faked in the harness and still need an in-game pass.
- Pixel-exact rendering. A CPU render of ImGui is for layout and text, not for matching the game's DX11 output.
- A native window, profiling, scenario files. Add them if we miss them.
- Shipping in the release package. The harness is a separate project and never ends up in the Dalamud zip.

## Phases

Each phase ships on its own and is useful on its own.

### A. Pure-logic extraction

Move decisions out of the draw code into plain functions covered by `VenueManager.Tests`, which already compiles single source files by link.

First candidates, all from `ShiftsTab` and the tabs touched by open PRs:

- Date separators: `ShouldBreak(DateTime? last, DateTime? next)`. A row with an unparseable date draws no separator and does not update the tracker.
- One `StartDate(string iso)` replacing `GetOpenShiftStartDate` and `GetShiftStartDate`.
- Open-shift visibility against the caller's roles.

Rule for the rest: extract only when a tab change would otherwise have no check. No sweep.

### C1. Headless host, text tree and tap

A console project `VenueManager.Harness`. It builds the real `Plugin` against fake Dalamud services, steps ImGui frames, and reads the frame back as text.

- `tree`: every visible text line with its rect, read from `ImDrawData`.
- `tap "Claim"` or `tap X Y`: injects a mouse click and steps frames.
- `type`, `key`, `step`, `state`, `tab NAME`, `command TEXT` (runs a registered chat command such as `/xvm sale 500`).

Output is plain text, so a Claude session can assert on it with no image reading.

### C2. Screenshots

A small CPU rasterizer for ImGui draw lists (triangles, vertex colours, font atlas texture), written to PNG. `shot` crops to the plugin window.

### C3. Real backend

The harness points `XvmApiReads` and the write client at the local dev xvm-api with seeded data, using a pairing secret from an environment variable. Link, Claim, clock-in and sales then run end to end. Config lives in a harness-only folder so a real secret is never touched.

## Architecture

```
VenueManager.Harness (console, net10.0-windows7.0, EnableWindowsTargeting)
  Host/      builds Plugin, sets the static services, steps frames
  Fakes/     one fake per Dalamud service the plugin consumes
  Driver/    CLI verbs: tree, tap, type, key, step, state, tab, command, shot
  Render/    C2 only: draw list to PNG
```

The harness references `VenueManager.csproj`. `Plugin` takes its services through static `[PluginService]` properties, so the host sets them by reflection and then calls `new Plugin()`.

### Fakes

| Service | Behaviour |
| --- | --- |
| `IDalamudPluginInterface` | `GetPluginConfig` and `SavePluginConfig` in a harness folder, `UiBuilder` with real `Draw`, `OpenMainUi` and `OpenConfigUi` events, `AssemblyLocation` |
| `IClientState` | `TerritoryType`, `Logout` and `TerritoryChanged` events, settable from the driver |
| `IFramework` | `Update` event raised once per stepped frame |
| `ICommandManager` | records handlers so `command TEXT` can invoke them |
| `IDtrBar` and `IDtrBarEntry` | records text and visibility |
| `INotificationManager` | records notifications, readable through `state` |
| `IChatGui`, `IPluginLog` | log to the harness console |
| `ITargetManager`, `IPlayerState`, `IObjectTable` | settable character name and target, empty object table |
| `IDataManager`, `ITextureProvider` | gated, see below |

Most interface members are unused. A `DispatchProxy` per interface, with handlers only for the members the plugin calls, keeps each fake short.

### Game-dependent call sites

Five places in `UI/` touch the game:

- Icon font: `ImGui.PushFont(UiBuilder.IconFont)` and `ImGuiComponents.IconButton`. `UiBuilder.IconFont` waits for an in-game Dalamud service, so any draw of the sidebar hangs outside the game. Done: these calls go through `IconFont` (xvm-plugin-dev#30, merged), whose defaults call exactly what the code called before. The harness replaces its three delegates.
- `SalesTab`: `Plugin.TargetManager.Target`. Covered by the fake target.
- `SettingsTab:508`: `DataManager.GetExcelSheet<Map>()` and `AgentMap.Instance()`. Wrapped so the harness reports "no map data" instead of dereferencing game memory.
- `VenuesTab:34`: `TextureProvider.GetFromGameIcon`. The fake returns an empty wrap and the tab draws its fallback.
- `Plugin.cs` territory and object-table scans. Not exercised by the harness.

Each gate is a small, named seam in plugin code. No behaviour changes in game.

### Findings from the boot spike (2026-11-16)

- The real `VenueManager.csproj` builds as a project reference from Linux when the harness targets `net10.0-windows7.0` with `EnableWindowsTargeting`. The Dalamud assemblies it uses are not exposed through the reference, so the harness references them itself from the Dalamud folder.
- `new Plugin()` constructs against `DispatchProxy` fakes that return defaults. The constructor touches about 25 members across `IDalamudPluginInterface`, `IClientState`, `IFramework`, `ICommandManager`, `IDtrBar` and its entry, `IUiBuilder`, `IPluginLog`. `ConfigDirectory`, `AssemblyLocation` and `GetPluginConfig` need real answers (a temp folder, the plugin assembly, and null).
- `WindowSystem.Draw()` draws nothing outside the game, because it depends on Dalamud internals. The harness draws the windows itself: for each open `Window`, an ImGui window with its `WindowName`, then `PreDraw`, `Draw`, `PostDraw`. The window `Toggle()` event path also does not open windows, so the harness sets `IsOpen`.
- Expected logged errors, all caught by the plugin: Dalamud's safe file write needs Windows file APIs, so the venue list and config do not persist; NAudio needs `winmm`, so the doorbell does not load; the housing manager address is unresolved.
- With no account linked the main window shows the Settings tab. Other tabs need the plugin state a linked account produces, so the harness seeds it (C3) or sets the selected tab.

### Native ImGui and Dalamud

The harness loads the same managed Dalamud assemblies the plugin already builds against (`~/.xlcore/dalamud/Hooks/dev`), so it works with the game down. `Dalamud.Bindings.ImGui` needs a native `libcimgui` for Linux; the Windows `cimgui.dll` in that folder is no use here. Aetherphone's harness bootstrap already builds a matching `libcimgui.so` into `~/.aetherphone-harness/native`, and keeps its cached Dalamud current. On 2026-11-16 its `Dalamud.dll` and `Dalamud.Bindings.ImGui.dll` are byte-identical to the installed ones in `~/.xlcore/dalamud/Hooks/dev` (same md5), so the struct layouts match. The harness reuses that library, found through `AETHERPHONE_HARNESS_CACHE` or the default path, and fails with a clear message if the two Dalamud builds differ. The first task of C1 is still a bare frame loop through the bindings, before any fake is written.

## Data flow

Driver verb, then host injects input and steps N frames (`Framework.Update` raised, ImGui `NewFrame`, `UiBuilder.Draw`, render), then the driver reads the draw data and prints it.

## Error handling

Fake members the plugin calls but we did not implement throw `NotImplementedException` naming the member. This is deliberate: a silent default would hide a missing fake. `NAudio` doorbell loading already runs in a try block, so audio failing on Linux only logs a warning.

## Testing

- A: unit tests in `VenueManager.Tests`, including the unparseable-date case for separators.
- C1: a smoke test that boots the host, opens the main window, and asserts `tree` contains the tab names. Run in CI on Linux.
- C3 is exercised by hand against the local dev API, not in CI.

## Risks

- Dalamud API bumps can break a fake. The fakes live in one folder and fail at compile time, so drift is loud.
- The native library must match the Dalamud ImGui struct layout. Matches today. A Dalamud update that the Aetherphone cache has not picked up yet would break it, so the harness compares the two bindings files at start and reports the mismatch.
- Referencing a `net10.0-windows7.0` project from the harness needs `EnableWindowsTargeting`. Verify the build on Linux before relying on it.

## Order of work

1. A, in a PR of its own, after #27 merges.
2. C1 feasibility spike: bare ImGui frame loop through `Dalamud.Bindings.ImGui` on Linux.
3. C1 host, fakes, `tree`, `tap`, smoke test.
4. C2, then C3.
