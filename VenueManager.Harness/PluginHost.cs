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

    private static readonly string[] AllowedHosts = { "dev-api.xivvenuemanager.com", "localhost", "127.0.0.1" };

    private readonly ImGuiHost imgui;
    private readonly Plugin plugin;
    private readonly string configDir;
    private readonly Dictionary<string, CommandInfo> commands = new();
    private readonly List<string> logLines = new();
    private readonly Func<ImFontPtr> originalFont = IconFont.Font;
    private readonly Func<Dalamud.Interface.FontAwesomeIcon, bool> originalButton = IconFont.IconButton;
    private readonly Func<string, Dalamud.Interface.FontAwesomeIcon, bool> originalButtonWithId = IconFont.IconButtonWithId;
    private Window? open;

    public string ApiUrl { get; }
    public bool Linked => plugin.xivAppClient?.IsConfigured == true;
    public bool LinkRejected => plugin.xivAppClient?.LinkRejected == true;
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<string> Log => logLines;

    private PluginHost(Configuration config, int width, int height, string apiUrl)
    {
        ApiUrl = apiUrl;
        Width = width;
        Height = height;
        configDir = Path.Combine(Path.GetTempPath(), "vm-harness-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);

        Fake.Reset();
        Fake.Overrides["IDalamudPluginInterface.get_ConfigDirectory"] = _ => new DirectoryInfo(configDir);
        Fake.Overrides["IDalamudPluginInterface.get_AssemblyLocation"] = _ => new FileInfo(typeof(Plugin).Assembly.Location);
        Fake.Overrides["IDalamudPluginInterface.GetPluginConfig"] = _ => config;
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

        imgui = new ImGuiHost(width, height, FontPath("FontAwesomeFreeSolid.otf"), FontPath("NotoSansCJKjp-Medium.otf"));
        IconFont.Font = () => imgui.HasIconFont ? imgui.IconFontHandle : ImGui.GetFont();
        IconFont.IconButton = icon => IconButton(icon, $"##{icon}");
        IconFont.IconButtonWithId = (id, icon) => IconButton(icon, id);
        plugin = new Plugin();
        imgui.Draw = DrawOpenWindow;
    }

    public static PluginHost Start(HarnessOptions options, int width = 900, int height = 700)
    {
        if (!string.IsNullOrEmpty(options.ApiSecret) && string.IsNullOrWhiteSpace(options.ApiUrl))
            throw new ArgumentException("A secret needs an explicit ApiUrl, so the harness never contacts the production API by accident.", nameof(options));

        var config = ConfigFor(options);
        if (!string.IsNullOrEmpty(options.ApiUrl)) CheckApiUrl(options.ApiUrl);

        return new PluginHost(config, width, height, string.IsNullOrWhiteSpace(options.ApiUrl) ? XvmApiPairing.DevUrl : options.ApiUrl);
    }

    public static void CheckApiUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("The harness only talks to dev-api.xivvenuemanager.com, localhost or 127.0.0.1.");
    }

    public void ApplySession(string secret)
    {
        CheckApiUrl(ApiUrl);
        plugin.Configuration.xvmApiUrl = ApiUrl;
        plugin.Configuration.xvmApiSecret = secret;
        plugin.xivAppClient!.ConfigureXvm(ApiUrl, secret);
        _ = Task.Run(async () =>
        {
            try { await plugin.AutoLoadXivAppDataAsync(); } catch { }
        });
    }

    public void ClearSession()
    {
        plugin.Configuration.xvmApiSecret = "";
        plugin.xivAppClient?.ClearXvm();
    }

    public ImGuiHost Imgui => imgui;

    public Configuration Configuration => plugin.Configuration;

    public Plugin Plugin => plugin;

    public IReadOnlyList<(string Id, string Slug, string Name)> Venues =>
        plugin.xivAppVenues.Select(v => (v.Id, v.Slug, v.Name)).ToList();

    public bool SelectVenue(string key)
    {
        var venues = plugin.xivAppVenues.ToList();
        var venue = venues.FirstOrDefault(v => string.Equals(v.Id, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(v.Slug, key, StringComparison.OrdinalIgnoreCase));
        if (venue is null)
        {
            var named = venues.Where(v => string.Equals(v.Name, key, StringComparison.OrdinalIgnoreCase)).ToList();
            if (named.Count != 1) return false;
            venue = named[0];
        }

        plugin.Configuration.selectedVenueId = venue.Id;
        plugin.Configuration.Save();
        plugin.currentXivAppVenueId = venue.Id;
        plugin.ResetShiftForVenueChange();
        _ = Task.Run(async () =>
        {
            try { await plugin.LoadVenueDataAsync(venue.Id); } catch { }
        });
        return true;
    }

    public IReadOnlyList<string> Tabs => TabList().Select(TabName).ToList();

    public string CurrentTab => TabName(CurrentTabField().GetValue(MainWindow())!);

    public void Open(string nameContains)
    {
        var window = plugin.WindowSystem.Windows.OfType<Window>().FirstOrDefault(w => w.WindowName.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"No window containing '{nameContains}'");

        foreach (var other in plugin.WindowSystem.Windows) other.IsOpen = false;
        window.IsOpen = true;
        open = window;
    }

    public void Step(int frames = 1) => imgui.Step(frames);

    public IReadOnlyList<string> Tree() => imgui.ReadText()
        .OrderBy(r => (int)r.Y)
        .ThenBy(r => r.X)
        .Select(r => $"({r.X:0},{r.Y:0}) {IconNames(r.Text)}")
        .ToList();

    private static string IconNames(string text) => string.Concat(text.Select(c =>
    {
        if (c < 0xF000 || c > 0xF8FF) return c.ToString();
        var name = ((Dalamud.Interface.FontAwesomeIcon)c).ToString();
        return char.IsDigit(name[0]) ? $"[U+{(int)c:X4}]" : $"[{name}]";
    }));

    private static string FontPath(string file)
    {
        var root = Environment.GetEnvironmentVariable("AETHERPHONE_HARNESS_CACHE");
        if (string.IsNullOrEmpty(root)) root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aetherphone-harness");
        return Path.Combine(root, "assets", "UIRes", file);
    }

    private bool IconButton(Dalamud.Interface.FontAwesomeIcon icon, string id)
    {
        if (!imgui.HasIconFont) return ImGui.Button($"[{icon}]{id}");

        ImGui.PushFont(imgui.IconFontHandle);
        var clicked = ImGui.Button($"{Dalamud.Interface.FontAwesomeExtensions.ToIconString(icon)}{id}");
        ImGui.PopFont();
        return clicked;
    }

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
        var version = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "";
        var config = new Configuration
        {
            LastSeenVersion = version,
            xvmApiUrl = string.IsNullOrWhiteSpace(options.ApiUrl) ? XvmApiPairing.DevUrl : options.ApiUrl,
        };
        if (!string.IsNullOrEmpty(options.ApiSecret)) config.xvmApiSecret = options.ApiSecret;

        return config;
    }

    private object? Record(string level, object?[] args)
    {
        var parts = args.Select(a => a is object?[] values ? string.Join(",", values) : a?.ToString());
        logLines.Add($"{level}: {string.Join(" | ", parts)}");
        return null;
    }

    private Window MainWindow() => plugin.WindowSystem.Windows.OfType<Window>().First(w => w.WindowName.Contains("Main"));

    private FieldInfo CurrentTabField() => MainWindow().GetType().GetField("_currentTab", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private IEnumerable<object> TabList() =>
        ((System.Collections.IEnumerable)MainWindow().GetType().GetField("tabs", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(MainWindow())!).Cast<object>();

    private static string TabName(object tab) => (string)tab.GetType().GetProperty("Name")!.GetValue(tab)!;
}
