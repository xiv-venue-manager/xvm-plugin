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

    public unsafe IReadOnlyList<TextRun> ReadText()
    {
        var runs = new List<TextRun>();
        var data = ImGui.GetDrawData();
        if (data.IsNull) return runs;

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

            for (var i = 0; i + 5 < indices.Size;)
            {
                var a = vertices.Data[indices.Data[i]];
                var c = vertices.Data[indices.Data[i + 2]];
                var uv = (Math.Min(a.Uv.X, c.Uv.X), Math.Min(a.Uv.Y, c.Uv.Y));
                if (!glyphs.TryGetValue(uv, out var glyph))
                {
                    Flush();
                    i += 3;
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
                i += 6;
            }

            Flush();
        }

        return runs;
    }

    public void Dispose() => ImGui.DestroyContext();
}
