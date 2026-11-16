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
}
