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
