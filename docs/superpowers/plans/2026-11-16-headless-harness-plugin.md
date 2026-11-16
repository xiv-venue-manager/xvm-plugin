# Headless Harness: Boot the Real Plugin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run the real XIV Venue Manager plugin headless: boot it against fake Dalamud services, draw its windows through ImGui, read the screen as text, select tabs, run chat commands, take PNG screenshots, and optionally point it at a local API. Spec: `docs/superpowers/specs/2026-11-16-headless-harness-design.md`. Foundation: `docs/superpowers/plans/2026-11-16-headless-harness.md` (done: `ImGuiHost`, `Tap`, `ReadText`, `Preflight`).

**Architecture:** The harness project now targets `net10.0-windows7.0` (with `EnableWindowsTargeting`) and references `VenueManager.csproj`. `Fake` is a `DispatchProxy` that returns defaults and records calls. `PluginHost` sets the plugin's static `[PluginService]` properties to fakes, swaps the three `IconFont` delegates (merged in xvm-plugin-dev#30) for plain ones, constructs `Plugin`, and draws the plugin's windows itself, because Dalamud's `WindowSystem.Draw` draws nothing outside the game. A line-based `Cli` drives it. A small CPU rasterizer writes PNGs.

**Tech Stack:** .NET 10, xUnit, `Dalamud.Bindings.ImGui`, DispatchProxy, `libcimgui.so` staged as `cimgui.dll`.

**Spike-verified on 2026-11-16 (scratch project, not committed):**
- The plugin project builds as a project reference from a `net10.0-windows7.0` console project on Linux, once the Dalamud assemblies are referenced explicitly (list below).
- `new Plugin()` constructs against the lenient fakes. The main window draws with the sidebar, header and Settings tab once the three `IconFont` delegates are swapped. The tab list is `Patrons, Sales, History, My Shift, Rooms, Bans, Inventory, Venues, Settings`, in a private `List<ITab> tabs` field of `MainWindow`; the selected tab is the private field `_currentTab`.
- With no account linked, `MainWindow.Draw` forces the Settings tab on every frame, so tab selection only sticks when a secret is configured.

**Not verified until the tasks run:** the exact member names of `CommandInfo`, `Notification` and `IFramework.Update` (tasks say what to do if a name differs), and anything about drawing the non-Settings tabs.

**Safety:** a configured secret makes the plugin contact the configured API URL, and the prod URL if none is set. `PluginHost` therefore refuses a secret without an explicit URL. Tests use `http://127.0.0.1:1`, which refuses connections. The harness never reads or writes the real plugin config: it uses a temporary folder. Do not tap "Open xivvenuemanager.com" or other link buttons, because the plugin opens links with the system browser.

Work in `~/xvm-plugin-harness` on branch `feat/harness`.

---

### Task 8: Retarget the harness and reference the plugin

**Files:**
- Modify: `VenueManager.Harness/Harness.props`
- Modify: `VenueManager.Harness/VenueManager.Harness.csproj`

- [ ] **Step 1: Replace Harness.props**

Replace the whole file with:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0-windows7.0</TargetFramework>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
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
    <DalamudDlls Include="$(DalamudDir)/Dalamud.dll;$(DalamudDir)/Dalamud.Bindings.ImGui.dll;$(DalamudDir)/FFXIVClientStructs.dll;$(DalamudDir)/Lumina.dll;$(DalamudDir)/Lumina.Excel.dll;$(DalamudDir)/InteropGenerator.Runtime.dll;$(DalamudDir)/Newtonsoft.Json.dll;$(DalamudDir)/Serilog.dll;$(DalamudDir)/HexaGen.Runtime.dll" />
    <Reference Include="@(DalamudDlls)">
      <Private>true</Private>
    </Reference>
  </ItemGroup>

  <Target Name="StageCimgui" AfterTargets="Build">
    <Copy SourceFiles="$(HarnessCache)/native/libcimgui.so" DestinationFiles="$(OutDir)cimgui.dll" Condition="Exists('$(HarnessCache)/native/libcimgui.so')" />
  </Target>
</Project>
```

- [ ] **Step 2: Reference the plugin and make the harness an executable**

Replace `VenueManager.Harness/VenueManager.Harness.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="Harness.props" />
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <RootNamespace>VenueManager.Harness</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\VenueManager\VenueManager.csproj" />
  </ItemGroup>
</Project>
```

An executable needs an entry point. Create `VenueManager.Harness/Program.cs`:

```csharp
namespace VenueManager.Harness;

public static class Program
{
    public static int Main(string[] args) => 0;
}
```

(Task 11 replaces it.)

- [ ] **Step 3: Build and run the existing tests**

Run: `cd ~/xvm-plugin-harness && dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj --nologo -v q 2>&1 | tail -4`
Expected: `Passed!`, 10 tests. If the build fails on a missing Dalamud assembly name, run `ls ~/.xlcore/dalamud/Hooks/dev | grep -iE "^(Dalamud|FFXIV|Lumina|Interop|Newtonsoft|Serilog|HexaGen)"` and fix the `DalamudDlls` list to names that exist. Report any change.

- [ ] **Step 4: Confirm the plugin build is untouched**

Run: `dotnet build VenueManager/VenueManager.csproj -c Debug --nologo 2>&1 | grep -E "Warn|Error" | tail -2`
Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 5: Commit**

```bash
git add VenueManager.Harness
git commit -m "Point the harness at the real plugin project

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Task 9: Fake

**Files:**
- Create: `VenueManager.Harness/Fake.cs`
- Create: `VenueManager.Harness.Tests/FakeTests.cs`
- Modify: `docs/superpowers/specs/2026-11-16-headless-harness-design.md` (the Error handling paragraph)

- [ ] **Step 1: Write the failing tests**

Create `VenueManager.Harness.Tests/FakeTests.cs`:

```csharp
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using VenueManager.Harness;
using Xunit;

public class FakeTests
{
    public FakeTests() => Fake.Reset();

    [Fact]
    public void An_override_answers_a_property()
    {
        Fake.Overrides["IClientState.get_TerritoryType"] = _ => (ushort)7;

        var state = Fake.Make<IClientState>();

        Assert.Equal((ushort)7, state.TerritoryType);
    }

    [Fact]
    public void A_property_that_returns_an_interface_gets_a_nested_fake()
    {
        var plugin = Fake.Make<IDalamudPluginInterface>();

        Assert.NotNull(plugin.UiBuilder);
    }

    [Fact]
    public void An_event_handler_is_stored_and_can_be_raised()
    {
        var framework = Fake.Make<IFramework>();
        var runs = 0;
        framework.Update += _ => runs++;

        ((Fake)(object)framework).Raise("Update", framework);

        Assert.Equal(1, runs);
    }

    [Fact]
    public void A_removed_event_handler_is_not_raised()
    {
        var framework = Fake.Make<IFramework>();
        var runs = 0;
        IFramework.OnUpdateDelegate handler = _ => runs++;
        framework.Update += handler;
        framework.Update -= handler;

        ((Fake)(object)framework).Raise("Update", framework);

        Assert.Equal(0, runs);
    }

    [Fact]
    public void Every_call_is_recorded()
    {
        var state = Fake.Make<IClientState>();

        _ = state.TerritoryType;
        _ = state.TerritoryType;

        Assert.Equal(2, Fake.Calls["IClientState.get_TerritoryType"]);
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj --nologo -v q 2>&1 | tail -5`
Expected: build FAIL, `Fake` does not exist. If `IFramework.OnUpdateDelegate` does not exist, report the real delegate type name (reflect over `Dalamud.dll`) and use it.

- [ ] **Step 3: Write Fake**

Create `VenueManager.Harness/Fake.cs`:

```csharp
using System.Collections.Concurrent;
using System.Reflection;

namespace VenueManager.Harness;

public class Fake : DispatchProxy
{
    public static readonly ConcurrentDictionary<string, int> Calls = new();
    public static readonly Dictionary<string, Func<object?[], object?>> Overrides = new();

    private readonly Dictionary<string, Delegate?> events = new();
    private readonly Dictionary<string, object?> props = new();
    private Type iface = null!;

    public static void Reset()
    {
        Calls.Clear();
        Overrides.Clear();
    }

    public static T Make<T>() where T : class => (T)Make(typeof(T));

    public static object Make(Type type)
    {
        var proxy = Create(type, typeof(Fake));
        ((Fake)proxy).iface = type;
        return proxy;
    }

    public void Raise(string name, params object?[] args) => events.GetValueOrDefault(name)?.DynamicInvoke(args);

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        var name = method!.Name;
        var key = iface.Name + "." + name;
        Calls.AddOrUpdate(key, 1, (_, count) => count + 1);
        args ??= Array.Empty<object?>();

        if (Overrides.TryGetValue(key, out var answer)) return answer(args);

        if (name.StartsWith("add_"))
        {
            var eventName = name[4..];
            events[eventName] = Delegate.Combine(events.GetValueOrDefault(eventName), (Delegate?)args[0]);
            return null;
        }

        if (name.StartsWith("remove_"))
        {
            var eventName = name[7..];
            events[eventName] = Delegate.Remove(events.GetValueOrDefault(eventName), (Delegate?)args[0]);
            return null;
        }

        if (name.StartsWith("set_"))
        {
            props[name[4..]] = args[0];
            return null;
        }

        if (name.StartsWith("get_") && props.TryGetValue(name[4..], out var stored)) return stored;

        return DefaultFor(method.ReturnType);
    }

    private static object? DefaultFor(Type type)
    {
        if (type == typeof(void)) return null;
        if (type == typeof(string)) return "";
        if (type == typeof(Task)) return Task.CompletedTask;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var inner = type.GetGenericArguments()[0];
            var value = inner.IsValueType ? Activator.CreateInstance(inner) : null;
            return typeof(Task).GetMethod("FromResult")!.MakeGenericMethod(inner).Invoke(null, new[] { value });
        }

        if (type.IsInterface) return Make(type);
        if (type.IsValueType) return Activator.CreateInstance(type);
        return null;
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj --nologo -v q 2>&1 | tail -4`
Expected: `Passed!`, 15 tests.

- [ ] **Step 5: Update the spec's error-handling paragraph**

In `docs/superpowers/specs/2026-11-16-headless-harness-design.md`, find the paragraph under `## Error handling` that begins `Fake members the plugin calls but we did not implement throw` and replace the whole paragraph with:

```
Fakes are lenient: a member nobody overrode returns a default (empty string, completed task, a nested fake for an interface, zero for a value type) and every call is counted in `Fake.Calls`. The plugin's constructor needs defaults for members that have no meaningful answer, so throwing on unimplemented members is not workable. Tests assert on the call log where it matters. `NAudio` doorbell loading and Dalamud's safe file write fail on Linux; the plugin catches and logs both.
```

- [ ] **Step 6: Commit**

```bash
git add VenueManager.Harness VenueManager.Harness.Tests docs/superpowers/specs
git commit -m "Add the lenient Fake proxy for Dalamud services

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Task 10: PluginHost

**Files:**
- Create: `VenueManager.Harness/PluginHost.cs`
- Create: `VenueManager.Harness.Tests/PluginHostTests.cs`

`ImGuiHost` is a single global ImGui context and `PluginHost` owns one, so tests stay serial (already configured).

- [ ] **Step 1: Write the failing tests**

Create `VenueManager.Harness.Tests/PluginHostTests.cs`:

```csharp
using VenueManager.Harness;
using Xunit;

public class PluginHostTests
{
    private static readonly string[] TabNames =
        { "Patrons", "Sales", "History", "My Shift", "Rooms", "Bans", "Inventory", "Venues", "Settings" };

    private static HarnessOptions Linked => new("http://127.0.0.1:1", "test-secret");

    [Fact]
    public void Draws_the_main_window_with_the_setup_text()
    {
        using var host = PluginHost.Start(new HarnessOptions());

        host.Open("Main");
        host.Step(4);

        var tree = host.Tree();
        Assert.Contains(tree, line => line.Contains("XIV Venue Manager"));
        Assert.Contains(tree, line => line.Contains("Welcome to XIV Venue Manager Sync!"));
    }

    [Fact]
    public void Lists_the_plugin_tabs()
    {
        using var host = PluginHost.Start(new HarnessOptions());

        Assert.Equal(TabNames, host.Tabs);
    }

    [Fact]
    public void Selecting_a_tab_sticks_once_an_account_is_linked()
    {
        using var host = PluginHost.Start(Linked);
        host.Open("Main");
        host.Step(3);

        var selected = host.SelectTab("My Shift");
        host.Step(3);

        Assert.True(selected);
        Assert.Equal("My Shift", host.CurrentTab);
    }

    [Fact]
    public void Selecting_an_unknown_tab_returns_false()
    {
        using var host = PluginHost.Start(new HarnessOptions());

        Assert.False(host.SelectTab("Nope"));
    }

    [Fact]
    public void A_registered_chat_command_runs_and_an_unknown_one_does_not()
    {
        using var host = PluginHost.Start(new HarnessOptions());

        Assert.True(host.Command("/xvm"));
        Assert.False(host.Command("/definitely-not-a-command"));
    }

    [Fact]
    public void A_secret_without_a_url_is_refused()
    {
        Assert.Throws<ArgumentException>(() => PluginHost.Start(new HarnessOptions(null, "test-secret")));
    }

    [Fact]
    public void Sidebar_icons_draw_without_hanging()
    {
        using var host = PluginHost.Start(Linked);

        host.Open("Main");
        host.Step(4);

        Assert.True(host.Tree().Count > 5);
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj --nologo -v q 2>&1 | tail -5`
Expected: build FAIL, `PluginHost` and `HarnessOptions` do not exist.

- [ ] **Step 3: Write PluginHost**

Create `VenueManager.Harness/PluginHost.cs`:

```csharp
using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using VenueManager.UI;

namespace VenueManager.Harness;

public sealed record HarnessOptions(string? ApiUrl = null, string? ApiSecret = null);

public sealed class PluginHost : IDisposable
{
    private static readonly (string Property, Type Service)[] Services =
    {
        ("PluginInterface", typeof(IDalamudPluginInterface)),
        ("ClientState", typeof(IClientState)),
        ("Framework", typeof(IFramework)),
        ("DataManager", typeof(IDataManager)),
        ("TextureProvider", typeof(ITextureProvider)),
        ("Objects", typeof(IObjectTable)),
        ("PlayerState", typeof(IPlayerState)),
        ("Log", typeof(IPluginLog)),
        ("Chat", typeof(IChatGui)),
        ("TargetManager", typeof(ITargetManager)),
        ("CommandManager", typeof(ICommandManager)),
        ("DtrBar", typeof(IDtrBar)),
        ("Notifications", typeof(INotificationManager)),
    };

    private readonly ImGuiHost imgui;
    private readonly Plugin plugin;
    private readonly string configDir;
    private readonly Dictionary<string, CommandInfo> commands = new();
    private readonly List<string> logLines = new();
    private readonly Func<ImFontPtr> originalFont = IconFont.Font;
    private readonly Func<Dalamud.Interface.FontAwesomeIcon, bool> originalButton = IconFont.IconButton;
    private readonly Func<string, Dalamud.Interface.FontAwesomeIcon, bool> originalButtonWithId = IconFont.IconButtonWithId;
    private Window? open;

    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<string> Log => logLines;

    private PluginHost(HarnessOptions options, int width, int height)
    {
        Width = width;
        Height = height;
        configDir = Path.Combine(Path.GetTempPath(), "vm-harness-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);

        Fake.Reset();
        Fake.Overrides["IDalamudPluginInterface.get_ConfigDirectory"] = _ => new DirectoryInfo(configDir);
        Fake.Overrides["IDalamudPluginInterface.get_AssemblyLocation"] = _ => new FileInfo(typeof(Plugin).Assembly.Location);
        Fake.Overrides["IDalamudPluginInterface.GetPluginConfig"] = _ => ConfigFor(options);
        Fake.Overrides["ICommandManager.AddHandler"] = args =>
        {
            commands[(string)args[0]!] = (CommandInfo)args[1]!;
            return true;
        };
        Fake.Overrides["ICommandManager.RemoveHandler"] = args => commands.Remove((string)args[0]!);
        Fake.Overrides["IPluginLog.Error"] = args => Record("error", args);
        Fake.Overrides["IPluginLog.Warning"] = args => Record("warning", args);

        foreach (var (property, service) in Services)
        {
            typeof(Plugin).GetProperty(property, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
                .SetValue(null, Fake.Make(service));
        }

        IconFont.Font = () => ImGui.GetFont();
        IconFont.IconButton = icon => ImGui.Button($"[{icon}]##{icon}");
        IconFont.IconButtonWithId = (id, icon) => ImGui.Button($"[{icon}]{id}");

        imgui = new ImGuiHost(width, height);
        plugin = new Plugin();
        imgui.Draw = DrawOpenWindow;
    }

    public static PluginHost Start(HarnessOptions options, int width = 900, int height = 700)
    {
        if (!string.IsNullOrEmpty(options.ApiSecret) && string.IsNullOrWhiteSpace(options.ApiUrl))
            throw new ArgumentException("A secret needs an explicit ApiUrl, so the harness never contacts the production API by accident.", nameof(options));

        return new PluginHost(options, width, height);
    }

    public ImGuiHost Imgui => imgui;

    public IReadOnlyList<string> Tabs => TabList().Select(TabName).ToList();

    public string CurrentTab => TabName(CurrentTabField().GetValue(MainWindow())!);

    public void Open(string nameContains)
    {
        var window = plugin.WindowSystem.Windows.FirstOrDefault(w => w.WindowName.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"No window containing '{nameContains}'");

        foreach (var other in plugin.WindowSystem.Windows) other.IsOpen = false;
        window.IsOpen = true;
        open = window;
    }

    public void Step(int frames = 1) => imgui.Step(frames);

    public IReadOnlyList<string> Tree() => imgui.ReadText()
        .OrderBy(r => (int)r.Y)
        .ThenBy(r => r.X)
        .Select(r => $"({r.X:0},{r.Y:0}) {r.Text}")
        .ToList();

    public bool Tap(string text) => imgui.TapText(text);

    public bool SelectTab(string name)
    {
        var tab = TabList().FirstOrDefault(t => string.Equals(TabName(t), name, StringComparison.OrdinalIgnoreCase));
        if (tab is null) return false;

        CurrentTabField().SetValue(MainWindow(), tab);
        return true;
    }

    public bool Command(string line)
    {
        var match = commands.Keys
            .Where(key => line == key || line.StartsWith(key + " ", StringComparison.Ordinal))
            .OrderByDescending(key => key.Length)
            .FirstOrDefault();
        if (match is null) return false;

        var arguments = line.Length > match.Length ? line[(match.Length + 1)..] : "";
        commands[match].Handler(match, arguments);
        return true;
    }

    public void Dispose()
    {
        try { plugin.Dispose(); } catch { }
        imgui.Dispose();
        IconFont.Font = originalFont;
        IconFont.IconButton = originalButton;
        IconFont.IconButtonWithId = originalButtonWithId;
        try { Directory.Delete(configDir, true); } catch { }
    }

    private void DrawOpenWindow()
    {
        if (open is null || !open.IsOpen) return;

        ImGui.SetNextWindowPos(new Vector2(10, 10));
        ImGui.SetNextWindowSize(new Vector2(Width - 20, Height - 20));
        ImGui.Begin(open.WindowName);
        open.PreDraw();
        open.Draw();
        open.PostDraw();
        ImGui.End();
    }

    private static Configuration ConfigFor(HarnessOptions options)
    {
        var config = new Configuration { LastSeenVersion = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "" };
        if (!string.IsNullOrEmpty(options.ApiSecret))
        {
            config.xvmApiUrl = options.ApiUrl!;
            config.xvmApiSecret = options.ApiSecret;
        }

        return config;
    }

    private object? Record(string level, object?[] args)
    {
        var parts = args.Select(a => a is object?[] values ? string.Join(",", values) : a?.ToString());
        logLines.Add($"{level}: {string.Join(" | ", parts)}");
        return null;
    }

    private Window MainWindow() => plugin.WindowSystem.Windows.First(w => w.WindowName.Contains("Main"));

    private FieldInfo CurrentTabField() => MainWindow().GetType().GetField("_currentTab", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private IEnumerable<object> TabList() =>
        ((System.Collections.IEnumerable)MainWindow().GetType().GetField("tabs", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(MainWindow())!).Cast<object>();

    private static string TabName(object tab) => (string)tab.GetType().GetProperty("Name")!.GetValue(tab)!;
}
```

Notes for the implementer: `Configuration.LastSeenVersion` is set so the Changelog window does not open on first draw. If the changelog window still opens, set `LastSeenVersion` to what `Plugin.PluginVersion` returns (read its definition) and report. Dalamud API 15 member names (`CommandInfo.Handler`, `Window.PreDraw`, `Window.WindowName`) come from the spike and the plugin source; if one differs, fix the name, keep the behaviour, and report it.

- [ ] **Step 4: Run the tests**

Run: `dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj --nologo -v q 2>&1 | tail -6`
Expected: `Passed!`, 22 tests. If the run hangs (no output for 60 seconds), kill it and report which test: a hang means a draw call is waiting on a Dalamud service (like the icon font did). Report the test name; do not guess a fix.

- [ ] **Step 5: Commit**

```bash
git add VenueManager.Harness VenueManager.Harness.Tests
git commit -m "Boot the real plugin headless with PluginHost

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Task 11: Command line

**Files:**
- Create: `VenueManager.Harness/Cli.cs`
- Modify: `VenueManager.Harness/Program.cs`
- Create: `VenueManager.Harness.Tests/CliTests.cs`

The command line reads one command per line and writes plain text, so a person or a Claude session can pipe a script into it.

- [ ] **Step 1: Write the failing tests**

Create `VenueManager.Harness.Tests/CliTests.cs`:

```csharp
using VenueManager.Harness;
using Xunit;

public class CliTests
{
    private static string Run(string script, HarnessOptions? options = null)
    {
        using var host = PluginHost.Start(options ?? new HarnessOptions());
        var output = new StringWriter();
        new Cli(host).Run(new StringReader(script), output);
        return output.ToString();
    }

    [Fact]
    public void Open_then_tree_prints_the_screen()
    {
        var output = Run("open Main\ntree\nquit\n");

        Assert.Contains("Welcome to XIV Venue Manager Sync!", output);
    }

    [Fact]
    public void Tabs_lists_every_tab_one_per_line()
    {
        var output = Run("tabs\nquit\n");

        Assert.Contains("My Shift", output);
        Assert.Contains("Settings", output);
    }

    [Fact]
    public void Tab_reports_the_selection_and_unknown_tabs()
    {
        var output = Run("open Main\ntab My Shift\ntab Nope\nquit\n", new HarnessOptions("http://127.0.0.1:1", "test-secret"));

        Assert.Contains("tab: My Shift", output);
        Assert.Contains("no tab: Nope", output);
    }

    [Fact]
    public void Tap_reports_a_missing_label()
    {
        var output = Run("open Main\ntap Nothing Here\nquit\n");

        Assert.Contains("not found: Nothing Here", output);
    }

    [Fact]
    public void Command_reports_whether_a_handler_ran()
    {
        var output = Run("command /definitely-not-a-command\nquit\n");

        Assert.Contains("no handler", output);
    }

    [Fact]
    public void An_unknown_verb_is_reported()
    {
        var output = Run("frobnicate\nquit\n");

        Assert.Contains("unknown command: frobnicate", output);
    }

    [Fact]
    public void Quit_stops_reading()
    {
        var output = Run("quit\ntabs\n");

        Assert.DoesNotContain("Settings", output);
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj --nologo -v q 2>&1 | tail -5`
Expected: build FAIL, `Cli` does not exist.

- [ ] **Step 3: Write Cli**

Create `VenueManager.Harness/Cli.cs`:

```csharp
namespace VenueManager.Harness;

public sealed class Cli
{
    private readonly PluginHost host;

    public Cli(PluginHost host) => this.host = host;

    public void Run(TextReader input, TextWriter output)
    {
        string? line;
        while ((line = input.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            var space = trimmed.IndexOf(' ');
            var verb = space < 0 ? trimmed : trimmed[..space];
            var rest = space < 0 ? "" : trimmed[(space + 1)..].Trim();

            if (verb == "quit") return;
            Handle(verb, rest, output);
        }
    }

    private void Handle(string verb, string rest, TextWriter output)
    {
        switch (verb)
        {
            case "open":
                host.Open(rest.Length == 0 ? "Main" : rest);
                host.Step(4);
                output.WriteLine("ok");
                break;
            case "step":
                host.Step(int.TryParse(rest, out var frames) ? frames : 1);
                output.WriteLine("ok");
                break;
            case "tree":
                foreach (var row in host.Tree()) output.WriteLine(row);
                break;
            case "tabs":
                foreach (var name in host.Tabs) output.WriteLine(name);
                break;
            case "tab":
                if (host.SelectTab(rest))
                {
                    host.Step(3);
                    output.WriteLine($"tab: {rest}");
                }
                else
                {
                    output.WriteLine($"no tab: {rest}");
                }

                break;
            case "tap":
                output.WriteLine(host.Tap(rest) ? $"tapped: {rest}" : $"not found: {rest}");
                break;
            case "command":
                output.WriteLine(host.Command(rest) ? "ran" : "no handler");
                break;
            case "state":
                output.WriteLine($"tab: {host.CurrentTab}");
                output.WriteLine($"log lines: {host.Log.Count}");
                break;
            default:
                output.WriteLine($"unknown command: {verb}");
                break;
        }
    }
}
```

- [ ] **Step 4: Replace Program**

Replace `VenueManager.Harness/Program.cs` with:

```csharp
namespace VenueManager.Harness;

public static class Program
{
    public static int Main(string[] args)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var problem = Preflight.Check(
            Environment.GetEnvironmentVariable("DALAMUD_HOME") ?? Path.Combine(home, ".xlcore/dalamud/Hooks/dev"),
            Environment.GetEnvironmentVariable("AETHERPHONE_HARNESS_CACHE") ?? Path.Combine(home, ".aetherphone-harness"));
        if (problem is not null)
        {
            Console.Error.WriteLine(problem);
            return 1;
        }

        var options = new HarnessOptions(
            Environment.GetEnvironmentVariable("XVM_HARNESS_URL"),
            Environment.GetEnvironmentVariable("XVM_HARNESS_SECRET"));

        using var host = PluginHost.Start(options);
        new Cli(host).Run(Console.In, Console.Out);
        return 0;
    }
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj --nologo -v q 2>&1 | tail -4`
Expected: `Passed!`, 29 tests.

- [ ] **Step 6: Run it end to end**

Run: `cd ~/xvm-plugin-harness && printf 'open Main\ntabs\ntree\nquit\n' | dotnet run --project VenueManager.Harness 2>&1 | head -30`
Expected: `ok`, the tab names, then screen rows such as `(83,66) XIV Venue Manager`. Report the first 12 lines.

- [ ] **Step 7: Commit**

```bash
git add VenueManager.Harness VenueManager.Harness.Tests
git commit -m "Add the harness command line

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Task 12: Screenshots

**Files:**
- Create: `VenueManager.Harness/Png.cs`
- Create: `VenueManager.Harness/Rasterizer.cs`
- Modify: `VenueManager.Harness/ImGuiHost.cs`
- Modify: `VenueManager.Harness/Cli.cs`
- Modify: `VenueManager.Harness.Tests/CliTests.cs`
- Create: `VenueManager.Harness.Tests/ScreenshotTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `VenueManager.Harness.Tests/ScreenshotTests.cs`:

```csharp
using System.Numerics;
using Dalamud.Bindings.ImGui;
using VenueManager.Harness;
using Xunit;

public class ScreenshotTests
{
    private static ImGuiHost Spike()
    {
        var host = new ImGuiHost(400, 300)
        {
            Draw = () =>
            {
                ImGui.SetNextWindowPos(new Vector2(10, 10));
                ImGui.SetNextWindowSize(new Vector2(300, 200));
                ImGui.Begin("Spike");
                ImGui.Text("Hello harness");
                ImGui.Button("Claim");
                ImGui.End();
            }
        };
        host.Step(3);
        return host;
    }

    [Fact]
    public void Png_has_the_signature_and_the_right_size()
    {
        var png = Png.Encode(new byte[4 * 3 * 2], 3, 2);

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
        Assert.Equal(3, (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19]);
        Assert.Equal(2, (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);
    }

    [Fact]
    public void Screenshot_draws_something_and_leaves_the_corner_as_background()
    {
        using var host = Spike();

        var pixels = host.Screenshot();

        Assert.Equal(400 * 300 * 4, pixels.Length);
        var corner = (299 * 400 + 399) * 4;
        Assert.Equal(new byte[] { 14, 18, 28, 255 }, pixels[corner..(corner + 4)]);

        var changed = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 14 || pixels[i + 1] != 18 || pixels[i + 2] != 28) changed++;
        }

        Assert.True(changed > 2000, $"only {changed} pixels differ from the background");
    }

    [Fact]
    public void The_window_area_is_not_the_background_colour()
    {
        using var host = Spike();

        var pixels = host.Screenshot();

        var inside = (150 * 400 + 100) * 4;
        Assert.NotEqual(new byte[] { 14, 18, 28 }, pixels[inside..(inside + 3)]);
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj --nologo -v q 2>&1 | tail -5`
Expected: build FAIL, `Png` and `Screenshot` do not exist.

- [ ] **Step 3: Write Png**

Create `VenueManager.Harness/Png.cs`:

```csharp
using System.IO.Compression;

namespace VenueManager.Harness;

public static class Png
{
    private static readonly uint[] Table = BuildTable();

    public static byte[] Encode(byte[] rgba, int width, int height)
    {
        using var output = new MemoryStream();
        output.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        var header = new byte[13];
        WriteInt(header, 0, width);
        WriteInt(header, 4, height);
        header[8] = 8;
        header[9] = 6;
        Chunk(output, "IHDR", header);

        var raw = new byte[height * (1 + width * 4)];
        for (var y = 0; y < height; y++)
        {
            Buffer.BlockCopy(rgba, y * width * 4, raw, y * (1 + width * 4) + 1, width * 4);
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, true))
        {
            zlib.Write(raw, 0, raw.Length);
        }

        Chunk(output, "IDAT", compressed.ToArray());
        Chunk(output, "IEND", Array.Empty<byte>());
        return output.ToArray();
    }

    private static void Chunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        WriteInt(length, 0, data.Length);
        output.Write(length);

        var body = new byte[4 + data.Length];
        for (var i = 0; i < 4; i++) body[i] = (byte)type[i];
        Buffer.BlockCopy(data, 0, body, 4, data.Length);
        output.Write(body);

        var crc = new byte[4];
        WriteInt(crc, 0, (int)Crc(body));
        output.Write(crc);
    }

    private static void WriteInt(byte[] target, int offset, int value)
    {
        target[offset] = (byte)(value >> 24);
        target[offset + 1] = (byte)(value >> 16);
        target[offset + 2] = (byte)(value >> 8);
        target[offset + 3] = (byte)value;
    }

    private static uint Crc(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }

        return table;
    }
}
```

- [ ] **Step 4: Write Rasterizer**

Create `VenueManager.Harness/Rasterizer.cs`:

```csharp
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace VenueManager.Harness;

public static class Rasterizer
{
    private static readonly byte[] Background = { 14, 18, 28, 255 };

    public static unsafe byte[] Render(ImDrawDataPtr data, byte[] atlas, int atlasWidth, int atlasHeight, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4) Buffer.BlockCopy(Background, 0, pixels, i, 4);
        if (data.IsNull) return pixels;

        for (var l = 0; l < data.CmdListsCount; l++)
        {
            var list = new ImDrawListPtr(data.CmdLists[l]);
            var vertices = list.VtxBuffer;
            var indices = list.IdxBuffer;
            var commands = list.CmdBuffer;

            for (var c = 0; c < commands.Size; c++)
            {
                var command = commands.Data[c];
                if (command.UserCallback != null) continue;

                for (var e = 0; e + 2 < command.ElemCount; e += 3)
                {
                    var a = vertices.Data[command.VtxOffset + indices.Data[command.IdxOffset + e]];
                    var b = vertices.Data[command.VtxOffset + indices.Data[command.IdxOffset + e + 1]];
                    var d = vertices.Data[command.VtxOffset + indices.Data[command.IdxOffset + e + 2]];
                    Fill(pixels, width, height, atlas, atlasWidth, atlasHeight, command.ClipRect, a, b, d);
                }
            }
        }

        return pixels;
    }

    private static void Fill(byte[] pixels, int width, int height, byte[] atlas, int atlasWidth, int atlasHeight, Vector4 clip, ImDrawVert a, ImDrawVert b, ImDrawVert c)
    {
        var area = Edge(a.Pos, b.Pos, c.Pos);
        if (area == 0) return;

        var minX = Math.Max(Math.Max(0, (int)clip.X), (int)MathF.Floor(Math.Min(a.Pos.X, Math.Min(b.Pos.X, c.Pos.X))));
        var minY = Math.Max(Math.Max(0, (int)clip.Y), (int)MathF.Floor(Math.Min(a.Pos.Y, Math.Min(b.Pos.Y, c.Pos.Y))));
        var maxX = Math.Min(Math.Min(width - 1, (int)clip.Z), (int)MathF.Ceiling(Math.Max(a.Pos.X, Math.Max(b.Pos.X, c.Pos.X))));
        var maxY = Math.Min(Math.Min(height - 1, (int)clip.W), (int)MathF.Ceiling(Math.Max(a.Pos.Y, Math.Max(b.Pos.Y, c.Pos.Y))));

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var w0 = Edge(b.Pos, c.Pos, p) / area;
                var w1 = Edge(c.Pos, a.Pos, p) / area;
                var w2 = Edge(a.Pos, b.Pos, p) / area;
                if (w0 < 0 || w1 < 0 || w2 < 0) continue;

                var uv = a.Uv * w0 + b.Uv * w1 + c.Uv * w2;
                var tx = Math.Clamp((int)(uv.X * atlasWidth), 0, atlasWidth - 1);
                var ty = Math.Clamp((int)(uv.Y * atlasHeight), 0, atlasHeight - 1);
                var texel = (ty * atlasWidth + tx) * 4;

                var red = (Channel(a.Col, 0) * w0 + Channel(b.Col, 0) * w1 + Channel(c.Col, 0) * w2) * atlas[texel] / 255f;
                var green = (Channel(a.Col, 8) * w0 + Channel(b.Col, 8) * w1 + Channel(c.Col, 8) * w2) * atlas[texel + 1] / 255f;
                var blue = (Channel(a.Col, 16) * w0 + Channel(b.Col, 16) * w1 + Channel(c.Col, 16) * w2) * atlas[texel + 2] / 255f;
                var alpha = (Channel(a.Col, 24) * w0 + Channel(b.Col, 24) * w1 + Channel(c.Col, 24) * w2) * atlas[texel + 3] / 255f / 255f;

                var target = (y * width + x) * 4;
                pixels[target] = (byte)(red * alpha + pixels[target] * (1 - alpha));
                pixels[target + 1] = (byte)(green * alpha + pixels[target + 1] * (1 - alpha));
                pixels[target + 2] = (byte)(blue * alpha + pixels[target + 2] * (1 - alpha));
                pixels[target + 3] = 255;
            }
        }
    }

    private static float Edge(Vector2 a, Vector2 b, Vector2 p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

    private static float Channel(uint color, int shift) => (color >> shift) & 0xFF;
}
```

- [ ] **Step 5: Expose the atlas and Screenshot on ImGuiHost**

In `VenueManager.Harness/ImGuiHost.cs`:

Add these members after the `fontSize` field:

```csharp
    private readonly byte[] atlas;
    private readonly int atlasWidth;
    private readonly int atlasHeight;
    private readonly int width;
    private readonly int height;
```

In the constructor, remove the local declaration `int atlasWidth, atlasHeight;` and replace the line `io.Fonts.GetTexDataAsRGBA32(0, &pixels, &atlasWidth, &atlasHeight);` with:

```csharp
        int w, h;
        io.Fonts.GetTexDataAsRGBA32(0, &pixels, &w, &h);
        atlasWidth = w;
        atlasHeight = h;
        atlas = new ReadOnlySpan<byte>(pixels, w * h * 4).ToArray();
        this.width = width;
        this.height = height;
```

Add this method after `ReadText`:

```csharp
    public byte[] Screenshot() => Rasterizer.Render(ImGui.GetDrawData(), atlas, atlasWidth, atlasHeight, width, height);
```

- [ ] **Step 6: Add the shot verb**

In `VenueManager.Harness/Cli.cs`, add this case before `default`:

```csharp
            case "shot":
                File.WriteAllBytes(rest, Png.Encode(host.Imgui.Screenshot(), host.Width, host.Height));
                output.WriteLine($"wrote {rest}");
                break;
```

And add this test inside the class in `VenueManager.Harness.Tests/CliTests.cs`:

```csharp
    [Fact]
    public void Shot_writes_a_png_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "vm-shot-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            var output = Run($"open Main\nshot {path}\nquit\n");

            Assert.Contains($"wrote {path}", output);
            Assert.Equal(0x89, File.ReadAllBytes(path)[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }
```

- [ ] **Step 7: Run the tests**

Run: `dotnet test VenueManager.Harness.Tests/VenueManager.Harness.Tests.csproj --nologo -v q 2>&1 | tail -5`
Expected: `Passed!`, 33 tests. If a `Dalamud.Bindings.ImGui` member name used above differs (`ImDrawCmd.ClipRect`, `ElemCount`, `IdxOffset`, `VtxOffset`, `UserCallback`, `ImDrawVert.Col`), fix the name and report it. If a pixel test fails because colours are packed in another order, report the actual pixel values; do not change the assertions.

- [ ] **Step 8: Look at a real screenshot**

Run: `cd ~/xvm-plugin-harness && printf 'open Main\nshot /tmp/claude-1000/-home-ehno/b9bc0b6e-c74c-45fb-8831-f1756f016665/scratchpad/main.png\nquit\n' | dotnet run --project VenueManager.Harness 2>&1 | tail -3`
Expected: `wrote ...main.png`. Then open the file with the Read tool (it displays PNGs) and describe what you see: is there a window with a title, text, a sidebar of `?` boxes, and readable text? Report honestly if it looks wrong (inverted, black, shifted, tiled).

- [ ] **Step 9: Commit**

```bash
git add VenueManager.Harness VenueManager.Harness.Tests
git commit -m "Add PNG screenshots to the harness

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Task 13: Docs

**Files:**
- Modify: `docs/HARNESS.md`

- [ ] **Step 1: Replace the file**

Replace `docs/HARNESS.md` with:

````markdown
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
````

- [ ] **Step 2: Commit**

```bash
git add docs/HARNESS.md
git commit -m "Document the plugin harness and its commands

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Self-review

**Spec coverage.** C1: boot with fakes, windows drawn by the harness, `tree`, `tap`, `tab`, `command`, `step`, `state` (Tasks 8 to 11). The icon-font gate is done (plugin PR #30). The three other gates in the spec (target, map sheet, texture icons) are exercised only when those tabs draw; the lenient fakes return defaults, and drawing tabs beyond Settings is not asserted here beyond selection (see Not verified). C2: screenshots (Task 12). C3: environment-seeded secret and URL with the prod-contact guard (Tasks 10 and 11); a real API run is manual and not covered by tests. Docs: Task 13. A CI smoke test is out of scope here: the harness is outside the sln by design.

**Placeholders.** None.

**Type consistency.** `HarnessOptions(ApiUrl, ApiSecret)`, `PluginHost.Start/Open/Step/Tree/Tap/SelectTab/Command/Tabs/CurrentTab/Log/Imgui/Width/Height`, `Cli.Run(TextReader, TextWriter)`, `ImGuiHost.Screenshot()`, `Png.Encode(rgba, w, h)`, `Rasterizer.Render(...)` match across tasks and tests. Test counts: 10 existing, +5 (Task 9) = 15, +7 (Task 10) = 22, +7 (Task 11) = 29, +4 (Task 12: 3 screenshot tests + 1 shot test) = 33.
