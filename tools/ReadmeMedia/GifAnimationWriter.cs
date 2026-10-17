using System.IO;
using System.Windows.Media.Imaging;

namespace MyKa.Spoofer.ReadmeMedia;

/// <summary>Adds timing/loop blocks that the Windows GIF encoder drops; pixel compression stays with the codec.</summary>
internal static class GifAnimationWriter
{
    public static void Save(GifBitmapEncoder encoder, Stream output, ushort delay)
    {
        using var encoded = new MemoryStream();
        encoder.Save(encoded);
        var data = encoded.ToArray();
        if (data.Length < 13 || System.Text.Encoding.ASCII.GetString(data, 0, 3) != "GIF")
            throw new InvalidDataException("The Windows encoder did not return a GIF.");
        var position = 13 + ((data[10] & 0x80) == 0 ? 0 : 3 * (1 << ((data[10] & 7) + 1)));
        if (position > data.Length) throw new InvalidDataException("Incomplete GIF palette.");
        output.Write("GIF89a"u8);
        output.Write(data, 6, position - 6);
        output.Write(new byte[] { 0x21, 0xff, 11 });
        output.Write("NETSCAPE2.0"u8);
        output.Write(new byte[] { 3, 1, 0, 0, 0 }); // Infinite looping.
        var frames = 0;
        while (position < data.Length)
        {
            var start = position;
            switch (data[position])
            {
                case 0x2c:
                    if (position + 10 > data.Length) throw new InvalidDataException("Incomplete GIF descriptor.");
                    var palette = data[position + 9];
                    position += 10 + ((palette & 0x80) == 0 ? 0 : 3 * (1 << ((palette & 7) + 1)));
                    if (position >= data.Length) throw new InvalidDataException("Incomplete GIF image.");
                    position++; // LZW minimum code size.
                    SkipBlocks(data, ref position);
                    // Full opaque frames replace the complete canvas; retain it between frames.
                    output.Write(new byte[] { 0x21, 0xf9, 4, 4, (byte)delay, (byte)(delay >> 8), 0, 0 });
                    output.Write(data, start, position - start);
                    frames++;
                    break;
                case 0x21:
                    if (position + 2 > data.Length) throw new InvalidDataException("Incomplete GIF extension.");
                    var label = data[position + 1];
                    position += 2;
                    SkipBlocks(data, ref position);
                    // Replace any codec-provided timing/loop metadata rather than duplicating it.
                    if (label != 0xf9 && label != 0xff) output.Write(data, start, position - start);
                    break;
                case 0x3b:
                    if (frames != encoder.Frames.Count) throw new InvalidDataException("GIF frame count changed during encoding.");
                    output.WriteByte(0x3b);
                    return;
                default:
                    throw new InvalidDataException("Unexpected GIF block from Windows encoder.");
            }
        }
        throw new InvalidDataException("GIF trailer is missing.");
    }

    private static void SkipBlocks(byte[] data, ref int position)
    {
        while (position < data.Length)
        {
            var count = data[position++];
            if (count == 0) return;
            position += count;
            if (position > data.Length) break;
        }
        throw new InvalidDataException("Incomplete GIF data block.");
    }
}
