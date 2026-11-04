using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MonitorDimmer;

internal enum Brand
{
    Website,
    GitHub,
    Instagram,
    YouTube,
    Spotify,
    LinkedIn
}

/// <summary>Project, author and donation links (same as hushBar).</summary>
internal static class Links
{
    public const string Repo = "https://github.com/ardacanbakis/monitorDimmer";
    public const string Website = "https://ardacanbakis.com";
    public const string Coffee = "https://buymeacoffee.com/ardacanbakis";
    public const string Author = "Arda Canbakis";

    public static readonly (Brand Brand, string Label, string Url)[] Social =
    [
        (Brand.Website, "ardacanbakis.com", Website),
        (Brand.GitHub, "GitHub", "https://github.com/ardacanbakis"),
        (Brand.Instagram, "Instagram", "https://www.instagram.com/arda.canbakiss/"),
        (Brand.YouTube, "YouTube", "https://www.youtube.com/@arda.canbakis"),
        (Brand.Spotify, "Spotify", "https://open.spotify.com/user/11146430303"),
        (Brand.LinkedIn, "LinkedIn", "https://linkedin.com/in/ardacanbakis")
    ];

    public static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch
        {
            // No default browser or the shell refused; nothing useful to show.
        }
    }
}

internal static class AppInfo
{
    /// <summary>"2.1.0" — taken from the build, so it always matches the release tag.</summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var info = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Application.ProductVersion;
        var plus = info.IndexOf('+');
        return plus > 0 ? info[..plus] : info;
    }
}

/// <summary>A focusable, clickable control whose look is supplied by a delegate.</summary>
internal sealed class PaintButton : SurfaceControl
{
    private readonly Action<Graphics, PaintButton> _painter;

    public PaintButton(Ui ui, string accessibleName, Action<Graphics, PaintButton> painter) : base(ui)
    {
        _painter = painter;
        AccessibleRole = AccessibleRole.Link;
        AccessibleName = accessibleName;
    }

    public bool Hot => IsHovered;
    public bool Down => IsPressed;
    public float FocusRadius { get; set; } = 6;
    public Color Background => SurfaceColor;

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            e.Handled = true;
            OnClick(EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        _painter(e.Graphics, this);
        if (ShowFocus)
        {
            DrawFocusRing(e.Graphics, new RectangleF(1, 1, Width - 3, Height - 3), Ui.S(FocusRadius));
        }
    }
}

/// <summary>The About section: logo, version, credit, donation button and social links.</summary>
internal sealed class AboutCard : Card
{
    private static readonly Color Heart = Color.FromArgb(0xFF, 0x40, 0x60);
    private static readonly Color CoffeeYellow = Color.FromArgb(0xFF, 0xDD, 0x00);

    private readonly Bitmap _logoImage;
    private readonly PaintButton _logo;
    private readonly PaintButton _name;
    private readonly PaintButton _coffee;
    private readonly List<PaintButton> _social = [];
    private readonly List<(string Text, bool IsHeart, bool IsName)> _credit = [];
    private readonly string _coffeeText;
    private readonly string _donate;

    private int _titleY, _taglineY, _versionY, _creditY, _dividerY, _donateY, _donateHeight, _donateWidth, _findY;
    private readonly List<(string Text, Rectangle Bounds, bool IsHeart)> _creditLayout = [];

    public AboutCard(Ui ui, ToolTip tips) : base(ui)
    {
        _logoImage = Logo.Render(ui.S(72));
        _donate = ui.T("donateText");
        _coffeeText = "☕  " + ui.T("buyCoffee");

        _logo = new PaintButton(ui, ui.T("projectPage"), (g, button) =>
        {
            g.Clear(button.Background);
            if (button.Hot)
            {
                Draw.FillRound(g, new RectangleF(0.5f, 0.5f, button.Width - 1.5f, button.Height - 1.5f), Ui.S(16), button.Down ? Ui.P.Pressed : Ui.P.Subtle);
            }

            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(_logoImage, (button.Width - _logoImage.Width) / 2, (button.Height - _logoImage.Height) / 2);
        }) { Size = new Size(ui.S(88), ui.S(88)), FocusRadius = 16 };
        _logo.Click += (_, _) => Links.Open(Links.Repo);
        tips.SetToolTip(_logo, ui.T("projectPage"));

        var nameFont = ui.Font(13, semibold: true);
        _name = new PaintButton(ui, Links.Author, (g, button) =>
        {
            g.Clear(button.Background);
            var color = button.Hot ? Ui.P.AccentHover : Ui.P.Accent;
            Draw.Text(g, Links.Author, nameFont, button.ClientRectangle, color, button.Background, Draw.Center);
            if (button.Hot)
            {
                using var pen = new Pen(color, Math.Max(1f, Ui.Scale));
                var y = button.Height - Ui.S(3);
                g.DrawLine(pen, Ui.S(2), y, button.Width - Ui.S(2), y);
            }
        }) { Size = new Size(Draw.Measure(Links.Author, nameFont) + ui.S(4), ui.S(22)), FocusRadius = 3 };
        _name.Click += (_, _) => Links.Open(Links.Website);
        tips.SetToolTip(_name, "ardacanbakis.com");

        var coffeeFont = ui.Font(14, semibold: true);
        _coffee = new PaintButton(ui, ui.T("buyCoffee"), (g, button) =>
        {
            g.Clear(button.Background);
            var fill = button.Down ? Color.FromArgb(0xF0, 0xCC, 0x00) : button.Hot ? Color.FromArgb(0xFF, 0xE5, 0x33) : CoffeeYellow;
            var bounds = new RectangleF(0.5f, 0.5f, button.Width - 1.5f, button.Height - 1.5f);
            Draw.FillRound(g, bounds, bounds.Height / 2, fill);
            Draw.Text(g, _coffeeText, coffeeFont, button.ClientRectangle, Color.FromArgb(0x1B, 0x1B, 0x1B), fill, Draw.Center);
        }) { Size = new Size(Draw.Measure(_coffeeText, coffeeFont) + ui.S(44), ui.S(40)), FocusRadius = 20 };
        _coffee.Click += (_, _) => Links.Open(Links.Coffee);
        tips.SetToolTip(_coffee, "buymeacoffee.com/ardacanbakis");

        foreach (var (brand, label, url) in Links.Social)
        {
            var button = new PaintButton(ui, label, (g, b) =>
            {
                g.Clear(b.Background);
                if (b.Hot)
                {
                    Draw.FillRound(g, new RectangleF(0.5f, 0.5f, b.Width - 1.5f, b.Height - 1.5f), Ui.S(8), b.Down ? Ui.P.Pressed : Ui.P.Subtle);
                }

                var size = Ui.S(24);
                BrandIcons.Paint(g, brand, new RectangleF((b.Width - size) / 2f, (b.Height - size) / 2f, size, size), Ui.P, b.Hot ? (b.Down ? Ui.P.Pressed : Ui.P.Subtle) : b.Background);
            }) { Size = new Size(ui.S(42), ui.S(42)), FocusRadius = 8 };
            button.Click += (_, _) => Links.Open(url);
            tips.SetToolTip(button, label);
            _social.Add(button);
        }

        foreach (var part in Regex.Split(ui.T("createdBy"), @"(\{0\}|\{1\})"))
        {
            if (part.Length > 0)
            {
                _credit.Add((part, part == "{0}", part == "{1}"));
            }
        }

        _credit.Add((" © 2026", false, false));

        Controls.Add(_logo);
        Controls.Add(_name);
        Controls.Add(_coffee);
        Controls.AddRange(_social.ToArray());
        Arrange = _ => ArrangeContent();
        Painter = (_, g) => PaintContent(g);
    }

    private void ArrangeContent()
    {
        var ui = Ui;
        var center = Width / 2;
        var y = ui.S(22);

        _logo.Location = new Point(center - _logo.Width / 2, y);
        y += _logo.Height + ui.S(6);
        _titleY = y;
        y += ui.S(30);
        _taglineY = y;
        y += ui.S(22);
        _versionY = y;
        y += ui.S(20) + ui.S(14);

        // Credit line: text, a red heart and the clickable name, centred as one run.
        _creditY = y;
        _creditLayout.Clear();
        var font = ui.Font(13);
        var lineHeight = _name.Height;
        var widths = _credit.Select(part => part.IsName ? _name.Width : part.IsHeart ? ui.S(18) : Draw.Measure(part.Text, font)).ToList();
        var x = center - widths.Sum() / 2;
        for (var i = 0; i < _credit.Count; i++)
        {
            var (text, isHeart, isName) = _credit[i];
            if (isName)
            {
                _name.Location = new Point(x, y);
            }
            else
            {
                _creditLayout.Add((text, new Rectangle(x, y, widths[i] + ui.S(1), lineHeight), isHeart));
            }

            x += widths[i];
        }

        y += lineHeight + ui.S(18);
        _dividerY = y;
        y += ui.S(18);

        _donateWidth = Math.Min(Width - ui.S(64), ui.S(460));
        _donateHeight = TextRenderer.MeasureText(_donate, ui.Font(13), new Size(_donateWidth, int.MaxValue), DonateFlags).Height;
        _donateY = y;
        y += _donateHeight + ui.S(18);

        _coffee.Location = new Point(center - _coffee.Width / 2, y);
        y += _coffee.Height + ui.S(24);

        _findY = y;
        y += ui.S(18) + ui.S(8);
        var gap = ui.S(6);
        var rowWidth = _social.Sum(button => button.Width) + gap * (_social.Count - 1);
        x = center - rowWidth / 2;
        foreach (var button in _social)
        {
            button.Location = new Point(x, y);
            x += button.Width + gap;
        }

        y += ui.S(42) + ui.S(20);
        if (Height != y)
        {
            Height = y;
        }
    }

    private const TextFormatFlags DonateFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

    private void PaintContent(Graphics g)
    {
        var ui = Ui;
        var p = ui.P;
        var full = new Rectangle(ui.S(16), 0, Width - ui.S(32), 0);
        Rectangle Line(int y, int height) => full with { Y = y, Height = height };

        Draw.Text(g, "Monitor Dimmer", ui.DisplayFont(22), Line(_titleY, ui.S(30)), p.Text, p.Card, Draw.Center);
        Draw.Text(g, ui.T("aboutTagline"), ui.Font(13), Line(_taglineY, ui.S(22)), p.Muted, p.Card, Draw.Center);
        Draw.Text(g, ui.T("version", AppInfo.Version), ui.Font(12), Line(_versionY, ui.S(20)), p.Muted, p.Card, Draw.Center);

        foreach (var (text, bounds, isHeart) in _creditLayout)
        {
            if (isHeart)
            {
                var size = ui.S(12);
                FillHeart(g, new RectangleF(bounds.X + (bounds.Width - size) / 2f, bounds.Y + (bounds.Height - size) / 2f + ui.S(1), size, size));
            }
            else
            {
                Draw.Text(g, text, ui.Font(13), bounds, p.Muted, p.Card, Draw.Line);
            }
        }

        using (var pen = new Pen(p.Border))
        {
            g.DrawLine(pen, Width / 2 - ui.S(140), _dividerY, Width / 2 + ui.S(140), _dividerY);
        }

        var donate = new Rectangle((Width - _donateWidth) / 2, _donateY, _donateWidth, _donateHeight + ui.S(4));
        TextRenderer.DrawText(g, _donate, ui.Font(13), donate, p.Muted, p.Card, DonateFlags);

        Draw.Text(g, ui.T("findMe"), ui.Font(12), Line(_findY, ui.S(18)), p.Muted, p.Card, Draw.Center);
    }

    private static void FillHeart(Graphics g, RectangleF r)
    {
        PointF P(float x, float y) => new(r.X + x * r.Width, r.Y + y * r.Height);
        using var path = new GraphicsPath();
        path.AddBezier(P(0.5f, 0.95f), P(0.2f, 0.72f), P(0f, 0.54f), P(0f, 0.31f));
        path.AddBezier(P(0f, 0.31f), P(0f, 0.12f), P(0.14f, 0f), P(0.29f, 0f));
        path.AddBezier(P(0.29f, 0f), P(0.40f, 0f), P(0.47f, 0.07f), P(0.5f, 0.16f));
        path.AddBezier(P(0.5f, 0.16f), P(0.53f, 0.07f), P(0.60f, 0f), P(0.71f, 0f));
        path.AddBezier(P(0.71f, 0f), P(0.86f, 0f), P(1f, 0.12f), P(1f, 0.31f));
        path.AddBezier(P(1f, 0.31f), P(1f, 0.54f), P(0.8f, 0.72f), P(0.5f, 0.95f));
        path.CloseFigure();
        var mode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Heart);
        g.FillPath(brush, path);
        g.SmoothingMode = mode;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _logoImage.Dispose();
        }
    }
}

/// <summary>Small vector brand marks drawn with GDI+ (ported from hushBar's BrandIcons).</summary>
internal static class BrandIcons
{
    public static void Paint(Graphics g, Brand brand, RectangleF r, Palette palette, Color background)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var s = r.Width;
        PointF Pt(float x, float y) => new(r.X + x * s, r.Y + y * s);

        switch (brand)
        {
            case Brand.Website:
            {
                using var pen = new Pen(Color.FromArgb(0x2E, 0x85, 0xE6), s * 0.08f);
                var inset = s * 0.06f;
                var globe = RectangleF.Inflate(r, -inset, -inset);
                g.DrawEllipse(pen, globe);
                g.DrawEllipse(pen, globe.X + globe.Width * 0.28f, globe.Y, globe.Width * 0.44f, globe.Height);
                var radius = globe.Width / 2;
                var cx = globe.X + radius;
                var cy = globe.Y + radius;
                foreach (var dy in new[] { -radius * 0.45f, 0f, radius * 0.45f })
                {
                    var half = MathF.Sqrt(radius * radius - dy * dy);
                    g.DrawLine(pen, cx - half, cy + dy, cx + half, cy + dy);
                }

                break;
            }

            case Brand.GitHub:
            {
                using var path = new GraphicsPath();
                path.AddEllipse(r.X + 0.14f * s, r.Y + 0.30f * s, 0.72f * s, 0.62f * s);
                path.AddPolygon([Pt(0.20f, 0.42f), Pt(0.28f, 0.08f), Pt(0.47f, 0.31f)]);
                path.AddPolygon([Pt(0.80f, 0.42f), Pt(0.72f, 0.08f), Pt(0.53f, 0.31f)]);
                path.FillMode = FillMode.Winding;
                using var body = new SolidBrush(palette.Text);
                g.FillPath(body, path);
                using var eyes = new SolidBrush(background);
                var eye = s * 0.10f;
                g.FillEllipse(eyes, r.X + 0.36f * s - eye / 2, r.Y + 0.58f * s - eye / 2, eye, eye);
                g.FillEllipse(eyes, r.X + 0.64f * s - eye / 2, r.Y + 0.58f * s - eye / 2, eye, eye);
                break;
            }

            case Brand.Instagram:
            {
                using var gradient = new LinearGradientBrush(new PointF(r.Left, r.Bottom), new PointF(r.Right, r.Top), Color.Black, Color.Black)
                {
                    InterpolationColors = new ColorBlend
                    {
                        Colors = [Color.FromArgb(0x66, 0x4F, 0xD9), Color.FromArgb(0xD4, 0x2E, 0x6B), Color.FromArgb(0xFC, 0x9E, 0x42)],
                        Positions = [0f, 0.5f, 1f]
                    }
                };
                using var pen = new Pen(gradient, s * 0.11f);
                using (var frame = Draw.Round(new RectangleF(r.X + 0.09f * s, r.Y + 0.09f * s, 0.82f * s, 0.82f * s), s * 0.26f))
                {
                    g.DrawPath(pen, frame);
                }

                g.DrawEllipse(pen, r.X + 0.30f * s, r.Y + 0.30f * s, 0.40f * s, 0.40f * s);
                g.FillEllipse(gradient, r.X + 0.70f * s - 0.05f * s, r.Y + 0.30f * s - 0.05f * s, 0.10f * s, 0.10f * s);
                break;
            }

            case Brand.YouTube:
            {
                var height = s * 0.70f;
                using (var red = new SolidBrush(Color.FromArgb(0xFF, 0x00, 0x00)))
                using (var shape = Draw.Round(new RectangleF(r.X, r.Y + (s - height) / 2, s, height), s * 0.22f))
                {
                    g.FillPath(red, shape);
                }

                using var white = new SolidBrush(Color.White);
                g.FillPolygon(white, [Pt(0.41f, 0.37f), Pt(0.65f, 0.50f), Pt(0.41f, 0.63f)]);
                break;
            }

            case Brand.Spotify:
            {
                using (var green = new SolidBrush(Color.FromArgb(0x1D, 0xB9, 0x54)))
                {
                    g.FillEllipse(green, r);
                }

                using var pen = new Pen(Color.White, s * 0.09f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                foreach (var (start, control, end) in new[]
                {
                    (Pt(0.20f, 0.42f), Pt(0.50f, 0.27f), Pt(0.80f, 0.42f)),
                    (Pt(0.26f, 0.57f), Pt(0.50f, 0.44f), Pt(0.75f, 0.57f)),
                    (Pt(0.32f, 0.71f), Pt(0.50f, 0.60f), Pt(0.69f, 0.71f))
                })
                {
                    // Quadratic → cubic Bézier.
                    var c1 = new PointF(start.X + 2f / 3f * (control.X - start.X), start.Y + 2f / 3f * (control.Y - start.Y));
                    var c2 = new PointF(end.X + 2f / 3f * (control.X - end.X), end.Y + 2f / 3f * (control.Y - end.Y));
                    g.DrawBezier(pen, start, c1, c2, end);
                }

                break;
            }

            case Brand.LinkedIn:
            {
                using (var blue = new SolidBrush(Color.FromArgb(0x0A, 0x66, 0xC2)))
                using (var shape = Draw.Round(r, s * 0.20f))
                {
                    g.FillPath(blue, shape);
                }

                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using var font = new Font(FontFamilies.Semibold, s * 0.58f, FontStyle.Bold, GraphicsUnit.Pixel);
                using var white = new SolidBrush(Color.White);
                using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("in", font, white, new RectangleF(r.X, r.Y + s * 0.02f, s, s), format);
                break;
            }
        }

        g.Restore(state);
    }
}
