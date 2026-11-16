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

    [Fact]
    public void ReadText_keeps_reading_after_a_shape_with_an_odd_index_count()
    {
        using var host = new ImGuiHost(400, 300)
        {
            Draw = () =>
            {
                ImGui.SetNextWindowPos(new Vector2(10, 10));
                ImGui.Begin("Odd");
                ImGui.CollapsingHeader("Header");
                ImGui.Text("After the arrow");
                ImGui.End();
            }
        };
        host.Step(3);

        var texts = host.ReadText().Select(r => r.Text).ToList();

        Assert.Contains("Header", texts);
        Assert.Contains("After the arrow", texts);
    }

    [Fact]
    public void ReadText_is_empty_before_any_frame_has_been_drawn()
    {
        using var host = new ImGuiHost(400, 300) { Draw = DrawSpike };

        Assert.Empty(host.ReadText());
    }

    private static string FontPath => Path.Combine(
        Environment.GetEnvironmentVariable("AETHERPHONE_HARNESS_CACHE") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aetherphone-harness"),
        "assets", "UIRes", "FontAwesomeFreeSolid.otf");

    [Fact]
    public void ReadText_reads_icon_font_glyphs_as_private_use_characters()
    {
        using var host = new ImGuiHost(400, 300, FontPath);
        host.Draw = () =>
        {
            ImGui.SetNextWindowPos(new Vector2(10, 10));
            ImGui.Begin("Icons");
            ImGui.PushFont(host.IconFontHandle);
            ImGui.Text("\uF013");
            ImGui.PopFont();
            ImGui.End();
        };
        Assert.True(host.HasIconFont);

        host.Step(3);

        Assert.Contains(host.ReadText(), r => r.Text == "\uF013");
    }

    private static string TextFontPath => Path.Combine(
        Environment.GetEnvironmentVariable("AETHERPHONE_HARNESS_CACHE") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aetherphone-harness"),
        "assets", "UIRes", "NotoSansCJKjp-Medium.otf");

    [Fact]
    public void ReadText_reads_symbols_from_the_text_font()
    {
        using var host = new ImGuiHost(400, 300, iconFontPath: null, textFontPath: TextFontPath);
        host.Draw = () =>
        {
            ImGui.SetNextWindowPos(new Vector2(10, 10));
            ImGui.Begin("Symbols");
            ImGui.Text("\u2713 Link your account");
            ImGui.Text("\u25CF \u25CB");
            ImGui.End();
        };

        host.Step(3);

        var runs = host.ReadText();
        Assert.Contains(runs, r => r.Text == "\u2713 Link your account");
        Assert.Contains(runs, r => r.Text == "\u25CF \u25CB");
    }

    private static ImGuiHost InputHost(Action<string> write)
    {
        var text = "";
        var host = new ImGuiHost(400, 300);
        host.Draw = () =>
        {
            ImGui.SetNextWindowPos(new Vector2(10, 10));
            ImGui.SetNextWindowSize(new Vector2(300, 100));
            ImGui.Begin("Input");
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("##field", ref text, 64);
            ImGui.End();
            write(text);
        };
        return host;
    }

    private static void ClickField(ImGuiHost host)
    {
        host.Step(3);
        host.MouseMove(60, 55);
        host.Step(2);
        host.MouseButton(0, true);
        host.Step(2);
        host.MouseButton(0, false);
        host.Step(2);
    }

    [Fact]
    public void Raw_mouse_methods_click_a_button()
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
        var run = host.ReadText().Single(r => r.Text == "Claim");

        host.MouseMove(run.CenterX, run.CenterY);
        host.Step(2);
        host.MouseButton(0, true);
        host.Step(1);
        host.MouseButton(0, false);
        host.Step(2);

        Assert.Equal(1, claims);
    }

    [Fact]
    public void Char_types_into_a_focused_input()
    {
        var value = "";
        using var host = InputHost(v => value = v);
        ClickField(host);

        host.Char('h');
        host.Step(2);
        host.Char('i');
        host.Step(2);

        Assert.Equal("hi", value);
    }

    [Fact]
    public void Backspace_key_removes_the_last_typed_character()
    {
        var value = "";
        using var host = InputHost(v => value = v);
        ClickField(host);
        host.Char('h');
        host.Step(2);
        host.Char('i');
        host.Step(2);

        host.Key(ImGuiKey.Backspace, true);
        host.Step(2);
        host.Key(ImGuiKey.Backspace, false);
        host.Step(2);

        Assert.Equal("h", value);
    }

    [Fact]
    public void MouseWheel_scrolls_a_child_window()
    {
        var scroll = 0f;
        using var host = new ImGuiHost(400, 300)
        {
            Draw = () =>
            {
                ImGui.SetNextWindowPos(new Vector2(10, 10));
                ImGui.SetNextWindowSize(new Vector2(300, 200));
                ImGui.Begin("Scroll");
                if (ImGui.BeginChild("list", new Vector2(250, 100)))
                {
                    for (var i = 0; i < 50; i++) ImGui.Text($"Line {i}");
                    scroll = ImGui.GetScrollY();
                }
                ImGui.EndChild();
                ImGui.End();
            }
        };
        host.Step(3);
        host.MouseMove(60, 60);
        host.Step(2);

        host.MouseWheel(0, -3);
        host.Step(3);

        Assert.True(scroll > 0);
    }

    [Fact]
    public void DrawDataHash_is_zero_before_any_frame()
    {
        using var host = new ImGuiHost(400, 300) { Draw = DrawSpike };

        Assert.Equal(0UL, host.DrawDataHash());
    }

    [Fact]
    public void DrawDataHash_is_stable_for_an_unchanged_frame()
    {
        using var host = new ImGuiHost(400, 300) { Draw = DrawSpike };

        host.Step(3);
        var first = host.DrawDataHash();
        host.Step(1);

        Assert.NotEqual(0UL, first);
        Assert.Equal(first, host.DrawDataHash());
    }

    [Fact]
    public void DrawDataHash_changes_when_the_drawn_text_changes()
    {
        var label = "Hello harness";
        using var host = new ImGuiHost(400, 300)
        {
            Draw = () =>
            {
                ImGui.SetNextWindowPos(new Vector2(10, 10));
                ImGui.Begin("Spike");
                ImGui.Text(label);
                ImGui.End();
            },
        };

        host.Step(3);
        var before = host.DrawDataHash();
        label = "Goodbye harness";
        host.Step(1);

        Assert.NotEqual(before, host.DrawDataHash());
    }

    private static List<float> RecordDeltas(Action<ImGuiHost> run)
    {
        var deltas = new List<float>();
        using var host = new ImGuiHost(400, 300) { Draw = () => deltas.Add(ImGui.GetIO().DeltaTime) };
        run(host);
        return deltas;
    }

    [Fact]
    public void Step_with_delta_applies_it_to_every_frame()
    {
        var deltas = RecordDeltas(h => h.Step(2, 0.05f));

        Assert.Equal(new[] { 0.05f, 0.05f }, deltas);
    }

    [Fact]
    public void Step_without_delta_uses_one_sixtieth()
    {
        var deltas = RecordDeltas(h => h.Step(1));

        Assert.Equal(1f / 60f, deltas.Single(), 1e-6f);
    }

    [Fact]
    public void Step_clamps_the_delta()
    {
        var deltas = RecordDeltas(h =>
        {
            h.Step(1, 0f);
            h.Step(1, 5f);
        });

        Assert.Equal(1f / 240f, deltas[0], 1e-6f);
        Assert.Equal(0.1f, deltas[1], 1e-6f);
    }

    private static int BackspaceDeletions(float delta)
    {
        var value = "";
        using var host = InputHost(v => value = v);
        ClickField(host);
        for (var i = 0; i < 20; i++) host.Char('a');
        host.Step(2);
        var before = value.Length;

        host.Key(ImGuiKey.Backspace, true);
        host.Step(10, delta);

        return before - value.Length;
    }

    [Fact]
    public void Held_key_repeats_faster_with_larger_deltas()
    {
        Assert.True(BackspaceDeletions(0.1f) > BackspaceDeletions(0.01f));
    }
}
