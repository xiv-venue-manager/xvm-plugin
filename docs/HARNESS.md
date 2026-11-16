# Headless harness

Runs ImGui without the game so the UI can be checked on a machine that cannot run FFXIV, or while plugins are down after a patch. Design: `docs/superpowers/specs/2026-11-16-headless-harness-design.md`.

## What exists

- `VenueManager.Harness`: `ImGuiHost` (frame loop, `ReadText`, `Tap`, `TapText`) and `Preflight`.
- `VenueManager.Harness.Tests`: tests for both.

It does not load the plugin yet. Booting `Plugin` against fake Dalamud services is the next step.

## Requirements

- .NET 10 SDK.
- Dalamud's files, normally in `~/.xlcore/dalamud/Hooks/dev` (set `DALAMUD_HOME` to use another folder).
- `libcimgui.so` in `~/.aetherphone-harness/native`, built by Aetherphone's harness bootstrap (set `AETHERPHONE_HARNESS_CACHE` to use another cache). The build copies it next to the output as `cimgui.dll`, the name `Dalamud.Bindings.ImGui` looks for on every platform.

## Run the tests

```bash
dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj
```

The projects are not in `VenueManager.sln`, so the release build and CI do not need these files.

## When Dalamud updates

`Preflight.Check(installedDalamudDir, cacheRoot)` returns a message when the installed `Dalamud.Bindings.ImGui.dll` differs from the one in the cache. Re-run Aetherphone's harness bootstrap to refresh the cache.
