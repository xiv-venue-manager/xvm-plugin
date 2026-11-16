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
