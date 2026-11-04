using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace MonitorDimmer;

internal sealed record Palette(
    bool IsDark,
    Color Window,
    Color Card,
    Color Layer,
    Color Border,
    Color Text,
    Color Muted,
    Color Subtle,
    Color Pressed,
    Color Accent,
    Color AccentHover,
    Color AccentSoft,
    Color OnAccent,
    Color Track,
    Color ThumbOuter,
    Color Input,
    Color InputBorder,
    Color Danger)
{
    public static Palette Create(bool dark)
    {
        var (forLight, forDark) = DevicePreferences.AccentColors();
        // A grey system accent would make sliders invisible against their track; use the brand indigo instead.
        if (forDark is { } d && d.GetSaturation() < 0.15f)
        {
            forDark = null;
            forLight = null;
        }

        if (dark)
        {
            var accent = forDark ?? Color.FromArgb(0x8B, 0x91, 0xF8);
            var card = Color.FromArgb(0x2B, 0x2B, 0x2B);
            return new Palette(true,
                Window: Color.FromArgb(0x20, 0x20, 0x20),
                Card: card,
                Layer: Color.FromArgb(0x32, 0x32, 0x32),
                Border: Color.FromArgb(0x3A, 0x3A, 0x3A),
                Text: Color.White,
                Muted: Color.FromArgb(0xAB, 0xAB, 0xAB),
                Subtle: Color.FromArgb(0x3A, 0x3A, 0x3A),
                Pressed: Color.FromArgb(0x30, 0x30, 0x30),
                Accent: accent,
                AccentHover: Draw.Blend(accent, card, 0.14f),
                AccentSoft: Draw.Blend(accent, card, 0.82f),
                OnAccent: Draw.Luminance(accent) > 0.45 ? Color.Black : Color.White,
                Track: Color.FromArgb(0x6E, 0x6E, 0x6E),
                ThumbOuter: Color.FromArgb(0x45, 0x45, 0x45),
                Input: Color.FromArgb(0x2D, 0x2D, 0x2D),
                InputBorder: Color.FromArgb(0x48, 0x48, 0x48),
                Danger: Color.FromArgb(0xFF, 0x99, 0xA4));
        }

        var lightAccent = forLight ?? Color.FromArgb(0x4F, 0x46, 0xE5);
        if (Draw.Luminance(lightAccent) > 0.5)
        {
            lightAccent = Draw.Blend(lightAccent, Color.Black, 0.35f);
        }

        return new Palette(false,
            Window: Color.FromArgb(0xF3, 0xF3, 0xF3),
            Card: Color.White,
            Layer: Color.FromArgb(0xF7, 0xF7, 0xF7),
            Border: Color.FromArgb(0xE3, 0xE3, 0xE3),
            Text: Color.FromArgb(0x1B, 0x1B, 0x1B),
            Muted: Color.FromArgb(0x5F, 0x5F, 0x5F),
            Subtle: Color.FromArgb(0xEE, 0xEE, 0xEE),
            Pressed: Color.FromArgb(0xE4, 0xE4, 0xE4),
            Accent: lightAccent,
            AccentHover: Draw.Blend(lightAccent, Color.White, 0.12f),
            AccentSoft: Draw.Blend(lightAccent, Color.White, 0.88f),
            OnAccent: Draw.Luminance(lightAccent) > 0.55 ? Color.Black : Color.White,
            Track: Color.FromArgb(0xA0, 0xA0, 0xA0),
            ThumbOuter: Color.White,
            Input: Color.White,
            InputBorder: Color.FromArgb(0xD0, 0xD0, 0xD0),
            Danger: Color.FromArgb(0xC4, 0x2B, 0x1C));
    }
}

/// <summary>Font families, resolved once, with Windows 11 fonts when available.</summary>
internal static class FontFamilies
{
    private static readonly HashSet<string> Installed = LoadInstalled();

    public static readonly string Regular = Pick("Segoe UI Variable Text", "Segoe UI");
    public static readonly string Semibold = Pick("Segoe UI Variable Text Semibold", "Segoe UI Semibold");
    public static readonly string Display = Pick("Segoe UI Variable Display Semibold", "Segoe UI Semibold");
    public static readonly string Icons = Pick("Segoe Fluent Icons", "Segoe MDL2 Assets");

    private static HashSet<string> LoadInstalled()
    {
        using var collection = new InstalledFontCollection();
        return collection.Families.Select(family => family.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string Pick(string preferred, string fallback)
    {
        if (!Installed.Contains(preferred))
        {
            return fallback;
        }

        // GDI+ silently substitutes families it cannot load; only keep the preferred one if it really loads.
        using var probe = new Font(preferred, 10f, GraphicsUnit.Pixel);
        return probe.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase) ? preferred : fallback;
    }
}

internal static class Glyphs
{
    public const string Heart = "";
    public const string Settings = "";
    public const string Refresh = "";
    public const string Edit = "";
    public const string Identify = "";
    public const string Sun = "";
    public const string Moon = "";
    public const string Link = "";
    public const string Add = "";
    public const string Delete = "";
    public const string Check = "";
    public const string Power = "";
}

/// <summary>Everything a UI build needs: palette, DPI scale, fonts and translations.</summary>
internal sealed class Ui : IDisposable
{
    private readonly Dictionary<(string Family, float Size), Font> _fonts = [];

    public Ui(Palette palette, int dpi, string language)
    {
        P = palette;
        Dpi = dpi <= 0 ? 96 : dpi;
        Scale = Dpi / 96f;
        Language = language;
    }

    public Palette P { get; }
    public int Dpi { get; }
    public float Scale { get; }
    public string Language { get; }

    public int S(float value) => (int)Math.Round(value * Scale);

    public string T(string key, params object[] values) => Strings.T(Language, key, values);

    public Font Font(float px, bool semibold = false) => Get(semibold ? FontFamilies.Semibold : FontFamilies.Regular, px);
    public Font DisplayFont(float px) => Get(FontFamilies.Display, px);
    public Font IconFont(float px) => Get(FontFamilies.Icons, px);

    private Font Get(string family, float px)
    {
        var key = (family, px);
        if (!_fonts.TryGetValue(key, out var font))
        {
            font = new Font(family, px * Scale, FontStyle.Regular, GraphicsUnit.Pixel);
            _fonts[key] = font;
        }

        return font;
    }

    public void Dispose()
    {
        foreach (var font in _fonts.Values)
        {
            font.Dispose();
        }

        _fonts.Clear();
    }
}

internal static class Draw
{
    public const TextFormatFlags Line = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
    public const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
    public const TextFormatFlags Right = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
    public const TextFormatFlags Wrap = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

    public static GraphicsPath Round(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0.5f)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRound(Graphics g, RectangleF bounds, float radius, Color fill, Color? border = null)
    {
        var mode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Round(bounds, radius);
        using (var brush = new SolidBrush(fill))
        {
            g.FillPath(brush, path);
        }

        if (border is { } color)
        {
            using var pen = new Pen(color);
            g.DrawPath(pen, path);
        }

        g.SmoothingMode = mode;
    }

    public static void Text(Graphics g, string text, Font font, Rectangle bounds, Color color, Color background, TextFormatFlags flags = Line) =>
        TextRenderer.DrawText(g, text, font, bounds, color, background, flags);

    public static int Measure(string text, Font font) =>
        TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;

    public static Color Blend(Color color, Color toward, float amount) => Color.FromArgb(
        (int)Math.Round(color.R + (toward.R - color.R) * amount),
        (int)Math.Round(color.G + (toward.G - color.G) * amount),
        (int)Math.Round(color.B + (toward.B - color.B) * amount));

    public static double Luminance(Color color) => (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255d;
}
