using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace VenueManager.Harness;

public static class Rasterizer
{
    private static readonly byte[] Background = { 14, 18, 28, 255 };

    private struct Tri
    {
        public float Ax, Ay, Bx, By, Cx, Cy, Area;
        public int MinX, MaxX, MinY, MaxY;
        public Vector2 Ua, Ub, Uc;
        public float[] Colors;
        public bool SolidUv;
        public int Texel;
        public bool Exact;
    }

    public static unsafe byte[] Render(ImDrawDataPtr data, byte[] atlas, int atlasWidth, int atlasHeight, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4) Buffer.BlockCopy(Background, 0, pixels, i, 4);
        if (data.IsNull) return pixels;

        var triangles = new List<Tri>();
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
                    if (Prepare(atlasWidth, atlasHeight, width, height, command.ClipRect, a, b, d) is { } tri) triangles.Add(tri);
                }
            }
        }

        var bands = Math.Clamp(Environment.ProcessorCount, 1, height);
        var rows = (height + bands - 1) / bands;
        var all = triangles.ToArray();
        Parallel.For(0, bands, band =>
        {
            var top = band * rows;
            var bottom = Math.Min(height, top + rows) - 1;
            foreach (ref var tri in all.AsSpan())
            {
                if (tri.MaxY < top || tri.MinY > bottom) continue;
                Fill(pixels, width, atlas, atlasWidth, atlasHeight, in tri, Math.Max(tri.MinY, top), Math.Min(tri.MaxY, bottom));
            }
        });

        return pixels;
    }

    private static Tri? Prepare(int atlasWidth, int atlasHeight, int width, int height, Vector4 clip, ImDrawVert a, ImDrawVert b, ImDrawVert c)
    {
        var area = Edge(a.Pos, b.Pos, c.Pos);
        if (area == 0) return null;

        var minX = Math.Max(Math.Max(0, (int)clip.X), (int)MathF.Floor(Math.Min(a.Pos.X, Math.Min(b.Pos.X, c.Pos.X))));
        var minY = Math.Max(Math.Max(0, (int)clip.Y), (int)MathF.Floor(Math.Min(a.Pos.Y, Math.Min(b.Pos.Y, c.Pos.Y))));
        var maxX = Math.Min(Math.Min(width - 1, (int)clip.Z), (int)MathF.Ceiling(Math.Max(a.Pos.X, Math.Max(b.Pos.X, c.Pos.X))));
        var maxY = Math.Min(Math.Min(height - 1, (int)clip.W), (int)MathF.Ceiling(Math.Max(a.Pos.Y, Math.Max(b.Pos.Y, c.Pos.Y))));
        if (minX > maxX || minY > maxY) return null;

        var tri = new Tri
        {
            Ax = a.Pos.X, Ay = a.Pos.Y, Bx = b.Pos.X, By = b.Pos.Y, Cx = c.Pos.X, Cy = c.Pos.Y, Area = area,
            MinX = minX, MaxX = maxX, MinY = minY, MaxY = maxY,
            Ua = a.Uv, Ub = b.Uv, Uc = c.Uv,
            Colors = new[]
            {
                Channel(a.Col, 0), Channel(a.Col, 8), Channel(a.Col, 16), Channel(a.Col, 24),
                Channel(b.Col, 0), Channel(b.Col, 8), Channel(b.Col, 16), Channel(b.Col, 24),
                Channel(c.Col, 0), Channel(c.Col, 8), Channel(c.Col, 16), Channel(c.Col, 24),
            },
            Exact = float.IsFinite(area) && float.IsFinite(a.Pos.X + a.Pos.Y + b.Pos.X + b.Pos.Y + c.Pos.X + c.Pos.Y)
                && Math.Abs(a.Pos.X) < 1e6f && Math.Abs(a.Pos.Y) < 1e6f && Math.Abs(b.Pos.X) < 1e6f && Math.Abs(b.Pos.Y) < 1e6f
                && Math.Abs(c.Pos.X) < 1e6f && Math.Abs(c.Pos.Y) < 1e6f,
        };

        if (a.Uv == b.Uv && b.Uv == c.Uv)
        {
            var fx = a.Uv.X * atlasWidth;
            var fy = a.Uv.Y * atlasHeight;
            var gx = fx - MathF.Floor(fx);
            var gy = fy - MathF.Floor(fy);
            if (gx is > 0.01f and < 0.99f && gy is > 0.01f and < 0.99f)
            {
                var tx = Math.Clamp((int)fx, 0, atlasWidth - 1);
                var ty = Math.Clamp((int)fy, 0, atlasHeight - 1);
                tri.SolidUv = true;
                tri.Texel = (ty * atlasWidth + tx) * 4;
            }
        }

        return tri;
    }

    private static void Fill(byte[] pixels, int width, byte[] atlas, int atlasWidth, int atlasHeight, in Tri t, int fromY, int toY)
    {
        var colors = t.Colors;
        var sign = t.Area > 0 ? 1.0 : -1.0;
        var abx = t.Cx - t.Bx;
        var aby = t.Cy - t.By;
        var bcx = t.Ax - t.Cx;
        var bcy = t.Ay - t.Cy;
        var cax = t.Bx - t.Ax;
        var cay = t.By - t.Ay;
        var r0 = colors[0]; var g0 = colors[1]; var b0 = colors[2]; var a0 = colors[3];
        var r1 = colors[4]; var g1 = colors[5]; var b1 = colors[6]; var a1 = colors[7];
        var r2 = colors[8]; var g2 = colors[9]; var b2 = colors[10]; var a2 = colors[11];

        for (var y = fromY; y <= toY; y++)
        {
            var py = y + 0.5f;
            var lo = t.MinX;
            var hi = t.MaxX;
            if (t.Exact)
            {
                Narrow(t.Bx, t.By, t.Cx, t.Cy, py, sign, ref lo, ref hi);
                Narrow(t.Cx, t.Cy, t.Ax, t.Ay, py, sign, ref lo, ref hi);
                Narrow(t.Ax, t.Ay, t.Bx, t.By, py, sign, ref lo, ref hi);
                if (lo > hi) continue;
            }

            var rowBc = abx * (py - t.By);
            var rowCa = bcx * (py - t.Cy);
            var rowAb = cax * (py - t.Ay);

            for (var x = lo; x <= hi; x++)
            {
                var px = x + 0.5f;
                var w0 = (rowBc - aby * (px - t.Bx)) / t.Area;
                var w1 = (rowCa - bcy * (px - t.Cx)) / t.Area;
                var w2 = (rowAb - cay * (px - t.Ax)) / t.Area;
                if (w0 < 0 || w1 < 0 || w2 < 0) continue;

                int texel;
                if (t.SolidUv)
                {
                    texel = t.Texel;
                }
                else
                {
                    var uv = t.Ua * w0 + t.Ub * w1 + t.Uc * w2;
                    var tx = Math.Clamp((int)(uv.X * atlasWidth), 0, atlasWidth - 1);
                    var ty = Math.Clamp((int)(uv.Y * atlasHeight), 0, atlasHeight - 1);
                    texel = (ty * atlasWidth + tx) * 4;
                }

                var red = (r0 * w0 + r1 * w1 + r2 * w2) * atlas[texel] / 255f;
                var green = (g0 * w0 + g1 * w1 + g2 * w2) * atlas[texel + 1] / 255f;
                var blue = (b0 * w0 + b1 * w1 + b2 * w2) * atlas[texel + 2] / 255f;
                var alpha = (a0 * w0 + a1 * w1 + a2 * w2) * atlas[texel + 3] / 255f / 255f;

                var target = (y * width + x) * 4;
                pixels[target] = (byte)(red * alpha + pixels[target] * (1 - alpha));
                pixels[target + 1] = (byte)(green * alpha + pixels[target + 1] * (1 - alpha));
                pixels[target + 2] = (byte)(blue * alpha + pixels[target + 2] * (1 - alpha));
                pixels[target + 3] = 255;
            }
        }
    }

    private static void Narrow(float ux, float uy, float vx, float vy, float py, double sign, ref int lo, ref int hi)
    {
        var m = (double)vy - uy;
        if (m == 0) return;

        var k = ((double)vx - ux) * ((double)py - uy) + m * ux;
        var edge = Math.Clamp(k / m - 0.5, -1e9, 1e9);
        if (sign * m > 0) hi = Math.Min(hi, (int)Math.Floor(edge) + 1);
        else lo = Math.Max(lo, (int)Math.Ceiling(edge) - 1);
    }

    private static float Edge(Vector2 a, Vector2 b, Vector2 p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

    private static float Channel(uint color, int shift) => (color >> shift) & 0xFF;
}
