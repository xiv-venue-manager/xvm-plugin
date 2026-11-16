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
