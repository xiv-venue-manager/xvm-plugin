# Headless Harness Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pull the shift draw decisions into unit-tested functions (Phase A), and build the harness core that runs ImGui headless, reads the frame back as text and clicks by label (the base of Phase C1).

**Architecture:** Phase A adds two small pure classes linked into `VenueManager.Tests`, and `ShiftsTab` calls them. The harness core is a separate library `VenueManager.Harness` that loads Dalamud's `Dalamud.Bindings.ImGui.dll` and the Linux `libcimgui.so` that Aetherphone's harness bootstrap already builds. It does not reference the plugin yet.

**Tech Stack:** .NET 10, xUnit, `Dalamud.Bindings.ImGui` (ImGui 1.88), `libcimgui.so` staged as `cimgui.dll`.

**Follow-up plan, written after this one lands:** booting the real `Plugin` against fake Dalamud services (the fakes list comes from running `Plugin` and seeing which members it calls), `tab`, `command`, `state`, then C2 screenshots and C3 real backend. See `docs/superpowers/specs/2026-11-16-headless-harness-design.md`.

**Verified in a scratch spike on 2026-11-16 (the code below is from it):**
- `Dalamud.Bindings.ImGui` loads its native library through `HexaGen.NativeLibraryContext` from a file named `cimgui.dll` next to the assembly, even on Linux. A `DllImportResolver` does not work. Copy `libcimgui.so` to `cimgui.dll`.
- The bindings are marked Windows-only, which produces CA1416 warnings on a plain `net10.0` build. They run fine on Linux.
- Text is read back by matching each glyph quad's top-left UV to the font's glyph table. Quads of one text item are contiguous in the index buffer. Spaces have no quad and are found from the glyph advance.
- A click needs: mouse position event, two frames, button down, one frame, button up, two frames. Hover is only true after the position event has been processed by a frame.

---

## Part 1: Phase A, shift logic out of the draw code

Base: start from `origin/master` **after plugin PR #27 merges**, because this edits the `ShiftsTab` code #27 introduces.

```bash
cd ~/xvm-plugin-dev-graph
git fetch origin
git worktree add -b refactor/shift-tab-logic ~/xvm-plugin-shift-logic origin/master
cd ~/xvm-plugin-shift-logic
```

Confirm #27 is in: `grep -n "visibleOpenShifts" VenueManager/UI/Tabs/ShiftsTab.cs` must print lines. If it prints nothing, stop and wait for #27.

### Task 1: ShiftDates

**Files:**
- Create: `VenueManager/ShiftDates.cs`
- Create: `VenueManager.Tests/ShiftDatesTests.cs`
- Modify: `VenueManager.Tests/VenueManager.Tests.csproj` (add one `Compile` link)

- [ ] **Step 1: Write the failing test**

Create `VenueManager.Tests/ShiftDatesTests.cs`:

```csharp
using System.Globalization;
using VenueManager;
using Xunit;

public class ShiftDatesTests
{
    [Fact]
    public void LocalStart_returns_the_local_time_of_a_utc_string()
    {
        var expected = DateTimeOffset.Parse("2026-10-11T19:00:00Z", CultureInfo.InvariantCulture).LocalDateTime;

        Assert.Equal((DateTime?)expected, ShiftDates.LocalStart("2026-10-11T19:00:00Z"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a date")]
    public void LocalStart_is_null_when_the_string_does_not_parse(string iso)
    {
        Assert.Null(ShiftDates.LocalStart(iso));
    }

    [Fact]
    public void BreakBetween_is_true_when_the_calendar_day_changes()
    {
        Assert.True(ShiftDates.BreakBetween(new DateTime(2026, 10, 11, 23, 0, 0), new DateTime(2026, 10, 12, 1, 0, 0)));
    }

    [Fact]
    public void BreakBetween_is_false_within_one_day()
    {
        Assert.False(ShiftDates.BreakBetween(new DateTime(2026, 10, 11, 9, 0, 0), new DateTime(2026, 10, 11, 23, 0, 0)));
    }

    [Fact]
    public void BreakBetween_is_false_for_the_first_row()
    {
        Assert.False(ShiftDates.BreakBetween(null, new DateTime(2026, 10, 11, 9, 0, 0)));
    }

    [Fact]
    public void BreakBetween_is_false_when_the_next_date_is_unknown()
    {
        Assert.False(ShiftDates.BreakBetween(new DateTime(2026, 10, 11, 9, 0, 0), null));
    }
}
```

- [ ] **Step 2: Link the file into the test project**

In `VenueManager.Tests/VenueManager.Tests.csproj`, add this line next to the other `ShiftLabelPicker.cs` link:

```xml
    <Compile Include="..\VenueManager\ShiftDates.cs" Link="ShiftDates.cs" />
```

- [ ] **Step 3: Run it and see it fail**

Run: `dotnet test VenueManager.Tests/VenueManager.Tests.csproj --nologo -v q 2>&1 | tail -5`
Expected: build FAIL, `ShiftDates.cs` not found.

- [ ] **Step 4: Write the implementation**

Create `VenueManager/ShiftDates.cs`:

```csharp
using System;
using System.Globalization;

namespace VenueManager
{
  internal static class ShiftDates
  {
    public static DateTime? LocalStart(string iso) =>
      DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var utc)
        ? utc.ToLocalTime()
        : null;

    public static bool BreakBetween(DateTime? last, DateTime? next) =>
      last.HasValue && next.HasValue && last.Value.Date != next.Value.Date;
  }
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test VenueManager.Tests/VenueManager.Tests.csproj --nologo -v q 2>&1 | tail -3`
Expected: `Passed!` with 6 more tests than before.

- [ ] **Step 6: Commit**

```bash
git add VenueManager/ShiftDates.cs VenueManager.Tests/ShiftDatesTests.cs VenueManager.Tests/VenueManager.Tests.csproj
git commit -m "Add ShiftDates for shift start parsing and day breaks

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Task 2: OpenShiftBoard

**Files:**
- Create: `VenueManager/OpenShiftBoard.cs`
- Create: `VenueManager.Tests/OpenShiftBoardTests.cs`
- Modify: `VenueManager.Tests/VenueManager.Tests.csproj`

- [ ] **Step 1: Write the failing test**

Create `VenueManager.Tests/OpenShiftBoardTests.cs`:

```csharp
using VenueManager;
using Xunit;

public class OpenShiftBoardTests
{
    private static OpenShiftDto Open(string id, string? role, string start) =>
        new() { Id = id, RoleName = role, ScheduledStart = start };

    private static Role Held(string name) => new() { Id = name, Name = name };

    [Fact]
    public void Hides_shifts_for_roles_the_caller_does_not_hold()
    {
        var shifts = new[] { Open("1", "Bartender", "2026-10-11T19:00:00Z"), Open("2", "Host", "2026-10-11T20:00:00Z") };

        var visible = OpenShiftBoard.Visible(shifts, new[] { Held("Host") });

        Assert.Equal(new[] { "2" }, visible.Select(s => s.Id));
    }

    [Fact]
    public void Matches_the_role_name_ignoring_case()
    {
        var shifts = new[] { Open("1", "bartender", "2026-10-11T19:00:00Z") };

        Assert.Single(OpenShiftBoard.Visible(shifts, new[] { Held("Bartender") }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Hides_shifts_with_no_role_name(string? role)
    {
        var shifts = new[] { Open("1", role, "2026-10-11T19:00:00Z") };

        Assert.Empty(OpenShiftBoard.Visible(shifts, new[] { Held("Bartender") }));
    }

    [Fact]
    public void Shows_nothing_when_the_caller_holds_no_roles()
    {
        var shifts = new[] { Open("1", "Bartender", "2026-10-11T19:00:00Z") };

        Assert.Empty(OpenShiftBoard.Visible(shifts, Array.Empty<Role>()));
    }

    [Fact]
    public void Orders_by_start_time()
    {
        var shifts = new[] { Open("late", "Host", "2026-10-12T19:00:00Z"), Open("early", "Host", "2026-10-11T19:00:00Z") };

        var visible = OpenShiftBoard.Visible(shifts, new[] { Held("Host") });

        Assert.Equal(new[] { "early", "late" }, visible.Select(s => s.Id));
    }

    [Fact]
    public void A_shift_with_an_unparseable_start_sorts_first()
    {
        var shifts = new[] { Open("ok", "Host", "2026-10-11T19:00:00Z"), Open("bad", "Host", "nope") };

        var visible = OpenShiftBoard.Visible(shifts, new[] { Held("Host") });

        Assert.Equal(new[] { "bad", "ok" }, visible.Select(s => s.Id));
    }
}
```

- [ ] **Step 2: Link the file**

Add to `VenueManager.Tests/VenueManager.Tests.csproj`:

```xml
    <Compile Include="..\VenueManager\OpenShiftBoard.cs" Link="OpenShiftBoard.cs" />
```

- [ ] **Step 3: Run it and see it fail**

Run: `dotnet test VenueManager.Tests/VenueManager.Tests.csproj --nologo -v q 2>&1 | tail -5`
Expected: build FAIL, `OpenShiftBoard.cs` not found.

- [ ] **Step 4: Write the implementation**

Create `VenueManager/OpenShiftBoard.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace VenueManager
{
  internal static class OpenShiftBoard
  {
    public static List<OpenShiftDto> Visible(IEnumerable<OpenShiftDto> open, IEnumerable<Role> roles)
    {
      var held = roles.Select(r => r.Name).ToList();
      return open
        .Where(s => !string.IsNullOrEmpty(s.RoleName) && held.Any(n => string.Equals(n, s.RoleName, StringComparison.OrdinalIgnoreCase)))
        .OrderBy(s => ShiftDates.LocalStart(s.ScheduledStart))
        .ToList();
    }
  }
}
```

`OpenShiftBoard.cs` calls `ShiftDates`, so the test project must also link `ShiftDates.cs`. That link was added in Task 1.

- [ ] **Step 5: Run the tests**

Run: `dotnet test VenueManager.Tests/VenueManager.Tests.csproj --nologo -v q 2>&1 | tail -3`
Expected: `Passed!`, 7 more tests than after Task 1.

- [ ] **Step 6: Commit**

```bash
git add VenueManager/OpenShiftBoard.cs VenueManager.Tests/OpenShiftBoardTests.cs VenueManager.Tests/VenueManager.Tests.csproj
git commit -m "Add OpenShiftBoard for the caller's visible open shifts

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Task 3: Use them in ShiftsTab

**Files:**
- Modify: `VenueManager/UI/Tabs/ShiftsTab.cs`

The draw code must keep exactly the same behaviour: a separator is drawn only when both the previous and the new date are known and the days differ, and the tracker only updates when the new date is known. Each section keeps its own tracker so a separator never crosses a section header.

- [ ] **Step 1: Replace the open-shifts filter**

Find:

```csharp
    var visibleOpenShifts = openShifts
      .Where(shift => !string.IsNullOrEmpty(shift.RoleName) && plugin.xivAppRoles.Any(role => string.Equals(role.Name, shift.RoleName, StringComparison.OrdinalIgnoreCase)))
      .OrderBy(GetOpenShiftStartDate)
      .ToList();
```

Replace with:

```csharp
    var visibleOpenShifts = OpenShiftBoard.Visible(openShifts, plugin.xivAppRoles);
```

- [ ] **Step 2: Replace the four separator blocks**

Open shifts. Find:

```csharp
      foreach (var shift in visibleOpenShifts)
      {
        var startDate = GetOpenShiftStartDate(shift);
        if (startDate.HasValue && lastOpenShiftDate.HasValue && startDate.Value.Date != lastOpenShiftDate.Value.Date)
        {
          ImGui.Spacing();
          ImGui.Separator();
        }
        if (startDate.HasValue) lastOpenShiftDate = startDate;
        drawOpenShiftRow(shift);
      }
```

Replace with:

```csharp
      foreach (var shift in visibleOpenShifts)
      {
        DrawDateBreak(ref lastOpenShiftDate, shift.ScheduledStart);
        drawOpenShiftRow(shift);
      }
```

Active. Find:

```csharp
      if (shift.Status == "ACTIVE")
      {
        var startDate = GetShiftStartDate(shift);
        if (lastDate.HasValue && startDate.HasValue && startDate.Value.Date != lastDate.Value.Date)
        {
          ImGui.Spacing();
          ImGui.Separator();
        }
        if (startDate.HasValue) lastDate = startDate;
        drawShiftRow(shift);
      }
```

Replace with:

```csharp
      if (shift.Status == "ACTIVE")
      {
        DrawDateBreak(ref lastDate, shift.ScheduledStart);
        drawShiftRow(shift);
      }
```

Upcoming. Find:

```csharp
      if (shift.Status == "SCHEDULED")
      {
        var startDate = GetShiftStartDate(shift);
        if (lastUpcomingDate.HasValue && startDate.HasValue && startDate.Value.Date != lastUpcomingDate.Value.Date)
        {
          ImGui.Spacing();
          ImGui.Separator();
        }
        if (startDate.HasValue) lastUpcomingDate = startDate;
        drawShiftRow(shift);
      }
```

Replace with:

```csharp
      if (shift.Status == "SCHEDULED")
      {
        DrawDateBreak(ref lastUpcomingDate, shift.ScheduledStart);
        drawShiftRow(shift);
      }
```

Completed. Find:

```csharp
      if (shift.Status == "COMPLETED")
      {
        var startDate = GetShiftStartDate(shift);
        if (lastCompletedDate.HasValue && startDate.HasValue && startDate.Value.Date != lastCompletedDate.Value.Date)
        {
          ImGui.Spacing();
          ImGui.Separator();
        }
        if (startDate.HasValue) lastCompletedDate = startDate;
        drawShiftRow(shift);
      }
```

Replace with:

```csharp
      if (shift.Status == "COMPLETED")
      {
        DrawDateBreak(ref lastCompletedDate, shift.ScheduledStart);
        drawShiftRow(shift);
      }
```

- [ ] **Step 3: Replace the two duplicate helpers**

Find and delete both of these:

```csharp
  private static DateTime? GetOpenShiftStartDate(OpenShiftDto shift)
  {
    return DateTime.TryParse(shift.ScheduledStart, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var startUtc)
      ? startUtc.ToLocalTime()
      : null;
  }

  private static DateTime? GetShiftStartDate(ShiftDto shift)
  {
    return DateTime.TryParse(shift.ScheduledStart, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var startUtc)
      ? startUtc.ToLocalTime()
      : null;
  }
```

In their place add:

```csharp
  private static void DrawDateBreak(ref DateTime? last, string scheduledStart)
  {
    var start = ShiftDates.LocalStart(scheduledStart);
    if (ShiftDates.BreakBetween(last, start))
    {
      ImGui.Spacing();
      ImGui.Separator();
    }
    if (start.HasValue) last = start;
  }
```

- [ ] **Step 4: Build and test**

Run: `dotnet build VenueManager/VenueManager.csproj -c Debug --nologo -v q 2>&1 | tail -3 && dotnet test VenueManager.Tests/VenueManager.Tests.csproj --nologo -v q 2>&1 | tail -3`
Expected: 0 warnings, 0 errors, then `Passed!`.

If the build reports `GetOpenShiftStartDate` or `GetShiftStartDate` is still used, run `grep -n "GetOpenShiftStartDate\|GetShiftStartDate" VenueManager/UI/Tabs/ShiftsTab.cs`, and replace each remaining call with `ShiftDates.LocalStart(<shift>.ScheduledStart)`.

- [ ] **Step 5: Check in game once**

The tests cover the two new classes. The draw wiring still has no automated check until the harness reaches `ShiftsTab`, so check it once in game: open the Shifts tab with shifts on at least two different days, and confirm a separator line sits between days in the Open, Upcoming and Completed sections and nowhere else. Same as before this change.

- [ ] **Step 6: Commit**

```bash
git add VenueManager/UI/Tabs/ShiftsTab.cs
git commit -m "Use ShiftDates and OpenShiftBoard in ShiftsTab

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Part 2: Harness core

Work in `~/xvm-plugin-harness` on branch `feat/harness` (the spec is already committed there). If #27 has merged and you want the harness branch current, run `git rebase origin/master` first.

### Task 4: Project scaffold and Preflight

**Files:**
- Create: `VenueManager.Harness/Harness.props`
- Create: `VenueManager.Harness/VenueManager.Harness.csproj`
- Create: `VenueManager.Harness/Preflight.cs`
- Create: `VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj`
- Create: `VenueManager.Harness.Tests/PreflightTests.cs`

The harness projects stay out of `VenueManager.sln`, so the release build and CI do not need Dalamud's Linux-specific files.

- [ ] **Step 1: Shared props**

Create `VenueManager.Harness/Harness.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <NoWarn>$(NoWarn);CA1416</NoWarn>
    <DalamudDir Condition="'$(DALAMUD_HOME)' != ''">$(DALAMUD_HOME)</DalamudDir>
    <DalamudDir Condition="'$(DalamudDir)' == ''">$(HOME)/.xlcore/dalamud/Hooks/dev</DalamudDir>
    <HarnessCache Condition="'$(AETHERPHONE_HARNESS_CACHE)' != ''">$(AETHERPHONE_HARNESS_CACHE)</HarnessCache>
    <HarnessCache Condition="'$(HarnessCache)' == ''">$(HOME)/.aetherphone-harness</HarnessCache>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="Dalamud.Bindings.ImGui">
      <HintPath>$(DalamudDir)/Dalamud.Bindings.ImGui.dll</HintPath>
      <Private>true</Private>
    </Reference>
  </ItemGroup>

  <Target Name="StageCimgui" AfterTargets="Build">
    <Copy SourceFiles="$(HarnessCache)/native/libcimgui.so" DestinationFiles="$(OutDir)cimgui.dll" Condition="Exists('$(HarnessCache)/native/libcimgui.so')" />
  </Target>
</Project>
```

- [ ] **Step 2: Library project**

Create `VenueManager.Harness/VenueManager.Harness.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="Harness.props" />
  <PropertyGroup>
    <RootNamespace>VenueManager.Harness</RootNamespace>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Test project**

Create `VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="..\VenueManager.Harness\Harness.props" />
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\VenueManager.Harness\VenueManager.Harness.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Write the failing test**

Create `VenueManager.Harness.Tests/PreflightTests.cs`:

```csharp
using VenueManager.Harness;
using Xunit;

public class PreflightTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "preflight-" + Guid.NewGuid().ToString("N"));

    private string Installed => Path.Combine(root, "installed");
    private string Cache => Path.Combine(root, "cache");

    private void Lay(string installedBytes, string cachedBytes, bool native = true)
    {
        Directory.CreateDirectory(Installed);
        Directory.CreateDirectory(Path.Combine(Cache, "dalamud"));
        Directory.CreateDirectory(Path.Combine(Cache, "native"));
        File.WriteAllText(Path.Combine(Installed, "Dalamud.Bindings.ImGui.dll"), installedBytes);
        File.WriteAllText(Path.Combine(Cache, "dalamud", "Dalamud.Bindings.ImGui.dll"), cachedBytes);
        if (native) File.WriteAllText(Path.Combine(Cache, "native", "libcimgui.so"), "native");
    }

    [Fact]
    public void Passes_when_the_bindings_are_identical()
    {
        Lay("same", "same");

        Assert.Null(Preflight.Check(Installed, Cache));
    }

    [Fact]
    public void Reports_a_mismatch_when_the_bindings_differ()
    {
        Lay("one", "two");

        Assert.Contains("differ", Preflight.Check(Installed, Cache));
    }

    [Fact]
    public void Reports_the_missing_file_by_path()
    {
        Lay("same", "same", native: false);

        Assert.Contains("libcimgui.so", Preflight.Check(Installed, Cache));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
```

- [ ] **Step 5: Run it and see it fail**

Run: `cd ~/xvm-plugin-harness && dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj --nologo -v q 2>&1 | tail -5`
Expected: build FAIL, `Preflight` does not exist.

- [ ] **Step 6: Write the implementation**

Create `VenueManager.Harness/Preflight.cs`:

```csharp
using System.Security.Cryptography;

namespace VenueManager.Harness;

public static class Preflight
{
    private const string Bindings = "Dalamud.Bindings.ImGui.dll";

    public static string? Check(string installedDalamudDir, string cacheRoot)
    {
        var installed = Path.Combine(installedDalamudDir, Bindings);
        var cached = Path.Combine(cacheRoot, "dalamud", Bindings);
        var native = Path.Combine(cacheRoot, "native", "libcimgui.so");

        foreach (var file in new[] { installed, cached, native })
        {
            if (!File.Exists(file)) return $"Missing {file}. Run the Aetherphone harness bootstrap.";
        }

        return Hash(installed) == Hash(cached)
            ? null
            : "The installed Dalamud ImGui bindings differ from the harness cache. Refresh the Aetherphone harness bootstrap, then retry.";
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
```

- [ ] **Step 7: Run the tests**

Run: `cd ~/xvm-plugin-harness && dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj --nologo -v q 2>&1 | tail -3`
Expected: `Passed!`, 3 tests.

- [ ] **Step 8: Commit**

```bash
git add VenueManager.Harness VenueManager.Harness.Tests
git commit -m "Add the harness project scaffold and Dalamud preflight check

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Task 5: ImGuiHost and text readback

**Files:**
- Create: `VenueManager.Harness/ImGuiHost.cs`
- Create: `VenueManager.Harness/TextRun.cs`
- Create: `VenueManager.Harness.Tests/AssemblyInfo.cs`
- Create: `VenueManager.Harness.Tests/ImGuiHostTests.cs`

ImGui keeps one global context, so tests must not run in parallel.

- [ ] **Step 1: Disable test parallelism**

Create `VenueManager.Harness.Tests/AssemblyInfo.cs`:

```csharp
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

- [ ] **Step 2: Write the failing test**

Create `VenueManager.Harness.Tests/ImGuiHostTests.cs`:

```csharp
using System.Numerics;
using Dalamud.Bindings.ImGui;
using VenueManager.Harness;
using Xunit;

public class ImGuiHostTests
{
    private static void DrawSpike()
    {
        ImGui.SetNextWindowPos(new Vector2(10, 10));
        ImGui.Begin("Spike");
        ImGui.Text("Hello harness");
        ImGui.Button("Claim");
        ImGui.End();
    }

    [Fact]
    public void ReadText_returns_each_text_item_whole_with_its_spaces()
    {
        using var host = new ImGuiHost(400, 300) { Draw = DrawSpike };

        host.Step(3);

        var texts = host.ReadText().Select(r => r.Text).ToList();
        Assert.Contains("Spike", texts);
        Assert.Contains("Hello harness", texts);
        Assert.Contains("Claim", texts);
    }

    [Fact]
    public void ReadText_gives_each_run_a_rectangle_on_screen()
    {
        using var host = new ImGuiHost(400, 300) { Draw = DrawSpike };

        host.Step(3);

        var hello = host.ReadText().Single(r => r.Text == "Hello harness");
        Assert.InRange(hello.X, 10, 400);
        Assert.True(hello.Right > hello.X);
        Assert.True(hello.Bottom > hello.Y);
    }
}
```

- [ ] **Step 3: Run it and see it fail**

Run: `cd ~/xvm-plugin-harness && dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj --nologo -v q 2>&1 | tail -5`
Expected: build FAIL, `ImGuiHost` does not exist.

- [ ] **Step 4: Write TextRun**

Create `VenueManager.Harness/TextRun.cs`:

```csharp
namespace VenueManager.Harness;

public sealed record TextRun(string Text, float X, float Y, float Right, float Bottom)
{
    public float CenterX => (X + Right) / 2;
    public float CenterY => (Y + Bottom) / 2;
}
```

- [ ] **Step 5: Write ImGuiHost**

Create `VenueManager.Harness/ImGuiHost.cs`:

```csharp
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;

namespace VenueManager.Harness;

public sealed class ImGuiHost : IDisposable
{
    private readonly ImGuiIOPtr io;
    private readonly Dictionary<(float, float), (uint Codepoint, float X0, float Advance)> glyphs = new();
    private readonly float fontSize;

    public Action Draw { get; set; } = () => { };

    public unsafe ImGuiHost(int width, int height)
    {
        ImGui.CreateContext();
        io = ImGui.GetIO();
        io.IniFilename = null;
        io.DisplaySize = new Vector2(width, height);
        io.DeltaTime = 1f / 60f;

        byte* pixels;
        int atlasWidth, atlasHeight;
        io.Fonts.GetTexDataAsRGBA32(0, &pixels, &atlasWidth, &atlasHeight);

        var font = io.Fonts.Fonts[0];
        fontSize = font.FontSize;
        var table = font.Glyphs;
        for (var i = 0; i < table.Size; i++)
        {
            var glyph = table.Data[i];
            if (glyph.Visible != 0) glyphs[(glyph.U0, glyph.V0)] = (glyph.Codepoint, glyph.X0, glyph.AdvanceX);
        }
    }

    public void Step(int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            ImGui.NewFrame();
            Draw();
            ImGui.Render();
        }
    }

    public unsafe IReadOnlyList<TextRun> ReadText()
    {
        var runs = new List<TextRun>();
        var data = ImGui.GetDrawData();

        for (var l = 0; l < data.CmdListsCount; l++)
        {
            var list = new ImDrawListPtr(data.CmdLists[l]);
            var vertices = list.VtxBuffer;
            var indices = list.IdxBuffer;

            var text = new StringBuilder();
            var open = false;
            float left0 = 0, top0 = 0, right0 = 0, bottom0 = 0, penEnd = 0, lastLeft = 0, lastBottom = 0;

            void Flush()
            {
                if (open) runs.Add(new TextRun(text.ToString(), left0, top0, right0, bottom0));
                text.Clear();
                open = false;
            }

            for (var i = 0; i + 5 < indices.Size; i += 6)
            {
                var a = vertices.Data[indices.Data[i]];
                var c = vertices.Data[indices.Data[i + 2]];
                var uv = (Math.Min(a.Uv.X, c.Uv.X), Math.Min(a.Uv.Y, c.Uv.Y));
                if (!glyphs.TryGetValue(uv, out var glyph))
                {
                    Flush();
                    continue;
                }

                var left = Math.Min(a.Pos.X, c.Pos.X);
                var top = Math.Min(a.Pos.Y, c.Pos.Y);
                var right = Math.Max(a.Pos.X, c.Pos.X);
                var bottom = Math.Max(a.Pos.Y, c.Pos.Y);

                if (open && (left < lastLeft || Math.Abs(bottom - lastBottom) > fontSize * 0.5f)) Flush();

                if (!open)
                {
                    left0 = left;
                    top0 = top;
                    bottom0 = bottom;
                    open = true;
                }
                else if (left - glyph.X0 - penEnd > 1.0f)
                {
                    text.Append(' ');
                }

                text.Append((char)glyph.Codepoint);
                top0 = Math.Min(top0, top);
                bottom0 = Math.Max(bottom0, bottom);
                right0 = right;
                penEnd = left - glyph.X0 + glyph.Advance;
                lastLeft = left;
                lastBottom = bottom;
            }

            Flush();
        }

        return runs;
    }

    public void Dispose() => ImGui.DestroyContext();
}
```

- [ ] **Step 6: Run the tests**

Run: `cd ~/xvm-plugin-harness && dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj --nologo -v q 2>&1 | tail -4`
Expected: `Passed!`, 5 tests. If it fails with `Unable to load shared library '.../cimgui.dll'`, the native file was not staged: run `ls ~/.aetherphone-harness/native/libcimgui.so` and `ls VenueManager.Harness.Tests/bin/Debug/net10.0/cimgui.dll`. If the first is missing, run Aetherphone's harness bootstrap (`dotnet run --project ~/plugin-research/aetherphone-gitlab/tools/harness/Bootstrap -c Release`).

- [ ] **Step 7: Commit**

```bash
git add VenueManager.Harness VenueManager.Harness.Tests
git commit -m "Add ImGuiHost with a frame loop and text readback

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Task 6: Clicking

**Files:**
- Modify: `VenueManager.Harness/ImGuiHost.cs`
- Modify: `VenueManager.Harness.Tests/ImGuiHostTests.cs`

- [ ] **Step 1: Write the failing tests**

Add inside `ImGuiHostTests`:

```csharp
    [Fact]
    public void TapText_clicks_the_button_with_that_label()
    {
        var claims = 0;
        using var host = new ImGuiHost(400, 300)
        {
            Draw = () =>
            {
                ImGui.SetNextWindowPos(new Vector2(10, 10));
                ImGui.Begin("Spike");
                if (ImGui.Button("Claim")) claims++;
                ImGui.End();
            }
        };
        host.Step(3);

        var found = host.TapText("Claim");

        Assert.True(found);
        Assert.Equal(1, claims);
    }

    [Fact]
    public void TapText_returns_false_when_no_text_matches()
    {
        using var host = new ImGuiHost(400, 300) { Draw = DrawSpike };
        host.Step(3);

        Assert.False(host.TapText("Nope"));
    }

    [Fact]
    public void A_tap_outside_any_button_does_not_click_it()
    {
        var claims = 0;
        using var host = new ImGuiHost(400, 300)
        {
            Draw = () =>
            {
                ImGui.SetNextWindowPos(new Vector2(10, 10));
                ImGui.Begin("Spike");
                if (ImGui.Button("Claim")) claims++;
                ImGui.End();
            }
        };
        host.Step(3);

        host.Tap(300, 250);

        Assert.Equal(0, claims);
    }
```

- [ ] **Step 2: Run them and see them fail**

Run: `cd ~/xvm-plugin-harness && dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj --nologo -v q 2>&1 | tail -5`
Expected: build FAIL, `Tap` and `TapText` do not exist.

- [ ] **Step 3: Write the implementation**

Add to `ImGuiHost` after `Step`:

```csharp
    public void Tap(float x, float y)
    {
        io.AddMousePosEvent(x, y);
        Step(2);
        io.AddMouseButtonEvent(0, true);
        Step(1);
        io.AddMouseButtonEvent(0, false);
        Step(2);
    }

    public bool TapText(string text)
    {
        var run = ReadText().FirstOrDefault(r => r.Text == text);
        if (run is null) return false;

        Tap(run.CenterX, run.CenterY);
        return true;
    }
```

- [ ] **Step 4: Run the tests**

Run: `cd ~/xvm-plugin-harness && dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj --nologo -v q 2>&1 | tail -4`
Expected: `Passed!`, 8 tests.

- [ ] **Step 5: Commit**

```bash
git add VenueManager.Harness VenueManager.Harness.Tests
git commit -m "Add Tap and TapText to ImGuiHost

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Task 7: How to run it

**Files:**
- Create: `docs/HARNESS.md`

- [ ] **Step 1: Write the doc**

Create `docs/HARNESS.md`:

````markdown
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
````

- [ ] **Step 2: Commit**

```bash
git add docs/HARNESS.md
git commit -m "Document how to run the harness tests

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Self-review

**Spec coverage.** Phase A (ShiftDates, OpenShiftBoard, ShiftsTab wiring) is Tasks 1 to 3. C1 pieces covered here: Linux native staging, Dalamud preflight, frame loop, `tree`-style text readback, tap by label (Tasks 4 to 6). Not covered, by design, and named in the header as the follow-up plan: booting `Plugin` with fakes, `type`, `key`, `state`, `tab`, `command`, the four game-dependent call-site gates, CI smoke test, C2 and C3.

**Placeholders.** None. Every code step has the code.

**Type consistency.** `ShiftDates.LocalStart` and `BreakBetween` (Task 1) are used with those names in Task 2 and Task 3. `ImGuiHost.Draw`, `Step`, `ReadText`, `Tap`, `TapText` and `TextRun.CenterX` and `CenterY` match between Tasks 5 and 6. `Preflight.Check(installedDalamudDir, cacheRoot)` matches its test and the doc.
