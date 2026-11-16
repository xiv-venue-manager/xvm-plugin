using System.Numerics;
using Dalamud.Bindings.ImGui;
using VenueManager.Harness;
using Xunit;

public class RasterizerTests
{
    private static unsafe (byte[] Atlas, int Width, int Height) Atlas()
    {
        byte* pixels;
        int w, h;
        ImGui.GetIO().Fonts.GetTexDataAsRGBA32(0, &pixels, &w, &h);
        return (new ReadOnlySpan<byte>(pixels, w * h * 4).ToArray(), w, h);
    }

    private static void AssertSame(int width, int height)
    {
        var (atlas, aw, ah) = Atlas();
        var expected = Reference.Render(ImGui.GetDrawData(), atlas, aw, ah, width, height);
        var actual = Rasterizer.Render(ImGui.GetDrawData(), atlas, aw, ah, width, height);
        Assert.Equal(expected.Length, actual.Length);
        var diff = -1;
        for (var i = 0; i < expected.Length; i++)
        {
            if (expected[i] != actual[i]) { diff = i; break; }
        }

        Assert.True(diff < 0, $"first difference at byte {diff} (pixel {diff / 4 % width},{diff / 4 / width})");
    }

    [Fact]
    public void Matches_the_reference_on_a_window_with_widgets()
    {
        var text = new byte[64];
        using var host = new ImGuiHost(480, 360)
        {
            Draw = () =>
            {
                ImGui.SetNextWindowPos(new Vector2(10, 10));
                ImGui.SetNextWindowSize(new Vector2(440, 330));
                ImGui.Begin("Reference");
                ImGui.Text("Hello rasterizer");
                ImGui.Separator();
                ImGui.Button("Claim");
                ImGui.Separator();
                ImGui.InputText("Name", text);
                ImGui.Separator();
                ImGui.BeginChild("rounded", new Vector2(300, 100), true);
                ImGui.Text("inside the child");
                ImGui.EndChild();
                ImGui.Separator();
                ImGui.End();
            }
        };
        host.Step(4);

        AssertSame(480, 360);
    }

    [Fact]
    public void Matches_the_reference_on_the_real_plugin_main_window()
    {
        using var host = PluginHost.Start(new HarnessOptions());
        host.Open("Main");
        host.Step(4);

        AssertSame(host.Width, host.Height);
    }

    private static class Reference
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
}
