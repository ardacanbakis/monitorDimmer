using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MonitorDimmer;

/// <summary>
/// The app mark: an indigo tile with a white monitor whose screen shows an amber
/// half-lit circle (◐ — "partly dimmed"). Small sizes drop the monitor and keep a bold ◐
/// so the tray icon stays readable at 16 px.
/// </summary>
internal static class Logo
{
    public static readonly int[] IcoSizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];

    private static readonly Color TileTop = Color.FromArgb(0x63, 0x66, 0xF1);
    private static readonly Color TileBottom = Color.FromArgb(0x1E, 0x1B, 0x4B);
    private static readonly Color Amber = Color.FromArgb(0xFB, 0xBF, 0x24);

    public static Bitmap Render(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        g.ScaleTransform(size / 64f, size / 64f);

        var small = size < 32;
        var tile = small ? new RectangleF(1, 1, 62, 62) : new RectangleF(2, 2, 60, 60);
        using (var tilePath = Draw.Round(tile, small ? 13 : 15))
        using (var gradient = new LinearGradientBrush(tile, TileTop, TileBottom, 65f))
        {
            g.FillPath(gradient, tilePath);
        }

        using var amber = new SolidBrush(Amber);
        if (small)
        {
            var circle = new RectangleF(15, 15, 34, 34);
            using var ring = new Pen(Amber, 6.5f);
            g.DrawEllipse(ring, circle);
            g.FillPie(amber, circle.X, circle.Y, circle.Width, circle.Height, 90, 180);
            return bitmap;
        }

        using (var white = new Pen(Color.White, 4f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            using var screen = Draw.Round(new RectangleF(11, 13, 42, 30), 5);
            g.DrawPath(white, screen);
            g.DrawLine(white, 32, 43, 32, 49);
            g.DrawLine(white, 23, 50.5f, 41, 50.5f);
        }

        var sun = new RectangleF(23, 19, 18, 18);
        using (var ring = new Pen(Amber, 2.6f))
        {
            g.DrawEllipse(ring, sun);
        }
        g.FillPie(amber, sun.X, sun.Y, sun.Width, sun.Height, 90, 180);
        return bitmap;
    }

    /// <summary>Builds a multi-resolution .ico (PNG frames) in memory.</summary>
    public static byte[] BuildIco(IReadOnlyList<int> sizes)
    {
        var frames = sizes.Select(size =>
        {
            using var bitmap = Render(size);
            using var png = new MemoryStream();
            bitmap.Save(png, ImageFormat.Png);
            return (Size: size, Data: png.ToArray());
        }).ToList();

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write((short)frames.Count);
        var offset = 6 + 16 * frames.Count;
        foreach (var (size, data) in frames)
        {
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write(data.Length);
            writer.Write(offset);
            offset += data.Length;
        }

        foreach (var (_, data) in frames)
        {
            writer.Write(data);
        }

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>A multi-frame icon for windows (Windows picks the small/large frames itself).</summary>
    public static Icon CreateIcon()
    {
        using var stream = new MemoryStream(BuildIco(IcoSizes));
        return new Icon(stream);
    }

    /// <summary>An icon rendered at exactly the requested pixel size (crisp tray icon at any DPI).</summary>
    public static Icon CreateIcon(Size size)
    {
        var sizes = IcoSizes.Append(size.Width).Distinct().OrderBy(value => value).ToArray();
        using var stream = new MemoryStream(BuildIco(sizes));
        return new Icon(stream, size);
    }
}
