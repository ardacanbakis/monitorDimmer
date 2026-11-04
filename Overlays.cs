namespace MonitorDimmer;

/// <summary>A borderless, click-through, never-activated top-most window.</summary>
internal abstract class OverlayForm : Form
{
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    protected OverlayForm()
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExTransparent | WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    protected override void WndProc(ref Message m)
    {
        // Keep exact physical bounds when a screen has a different scale factor.
        if (m.Msg == Native.WmDpiChanged)
        {
            m.Result = IntPtr.Zero;
            return;
        }

        if (m.Msg == Native.WmNcHitTest)
        {
            m.Result = new IntPtr(-1);
            return;
        }

        base.WndProc(ref m);
    }
}

/// <summary>Per-screen black overlays that dim screens beyond what hardware allows.</summary>
internal sealed class ScreenDimmer : IDisposable
{
    private readonly Dictionary<Rectangle, DimOverlay> _overlays = [];

    public void Apply(IEnumerable<(Rectangle Bounds, int Percent)> targets, bool rebuild = false)
    {
        var wanted = targets.Where(target => target.Percent > 0)
            .GroupBy(target => target.Bounds)
            .ToDictionary(group => group.Key, group => Math.Min(80, group.Max(target => target.Percent)));

        if (rebuild)
        {
            CloseAll();
        }

        foreach (var stale in _overlays.Keys.Where(bounds => !wanted.ContainsKey(bounds)).ToList())
        {
            _overlays[stale].Close();
            _overlays[stale].Dispose();
            _overlays.Remove(stale);
        }

        foreach (var (bounds, percent) in wanted)
        {
            if (_overlays.TryGetValue(bounds, out var overlay))
            {
                overlay.Opacity = percent / 100d;
            }
            else
            {
                overlay = new DimOverlay(bounds, percent / 100d);
                _overlays[bounds] = overlay;
                overlay.Show();
                overlay.Bounds = bounds;
            }
        }
    }

    private void CloseAll()
    {
        foreach (var overlay in _overlays.Values)
        {
            overlay.Close();
            overlay.Dispose();
        }

        _overlays.Clear();
    }

    public void Dispose() => CloseAll();

    private sealed class DimOverlay : OverlayForm
    {
        public DimOverlay(Rectangle bounds, double opacity)
        {
            Bounds = bounds;
            BackColor = Color.Black;
            Opacity = opacity;
        }

        protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Color.Black);
    }
}

internal sealed record IdentifyItem(Rectangle Screen, int Number, string Name, string Detail);

/// <summary>Shows a big number and name on each screen so you know which is which.</summary>
internal sealed class IdentifyOverlays : IDisposable
{
    private readonly List<IdentifyWindow> _windows = [];

    public void Show(IEnumerable<IdentifyItem> items, Palette palette)
    {
        CloseAll();
        foreach (var item in items)
        {
            var window = new IdentifyWindow(item, palette);
            window.FormClosed += (_, _) => _windows.Remove(window);
            _windows.Add(window);
            window.Show();
        }
    }

    private void CloseAll()
    {
        foreach (var window in _windows.ToList())
        {
            window.Close();
            window.Dispose();
        }

        _windows.Clear();
    }

    public void Dispose() => CloseAll();

    private sealed class IdentifyWindow : OverlayForm
    {
        private readonly IdentifyItem _item;
        private readonly Palette _palette;
        private readonly System.Windows.Forms.Timer _timer = new() { Interval = 2600 };
        private readonly Font _numberFont;
        private readonly Font _nameFont;
        private readonly Font _detailFont;

        public IdentifyWindow(IdentifyItem item, Palette palette)
        {
            _item = item;
            _palette = palette;
            var height = Math.Max(220, (int)(item.Screen.Height * 0.3));
            var width = Math.Min(item.Screen.Width - 40, (int)(height * 1.45));
            Bounds = new Rectangle(item.Screen.Left + (item.Screen.Width - width) / 2, item.Screen.Top + (item.Screen.Height - height) / 2, width, height);
            BackColor = palette.Card;
            Opacity = 0.97;
            _numberFont = new Font(FontFamilies.Display, height * 0.44f, GraphicsUnit.Pixel);
            _nameFont = new Font(FontFamilies.Semibold, height * 0.095f, GraphicsUnit.Pixel);
            _detailFont = new Font(FontFamilies.Regular, height * 0.065f, GraphicsUnit.Pixel);

            var fading = false;
            _timer.Tick += (_, _) =>
            {
                if (!fading)
                {
                    fading = true;
                    _timer.Interval = 25;
                }

                Opacity -= 0.08;
                if (Opacity <= 0.05)
                {
                    _timer.Stop();
                    Close();
                }
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Bounds = Bounds;
            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(_palette.Card);
            using (var pen = new Pen(_palette.Accent, Math.Max(3, Height / 60f)))
            {
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }

            var numberBounds = new Rectangle(0, (int)(Height * 0.06), Width, (int)(Height * 0.58));
            Draw.Text(g, _item.Number.ToString(), _numberFont, numberBounds, _palette.Accent, _palette.Card, Draw.Center);
            var nameBounds = new Rectangle(Height / 12, (int)(Height * 0.64), Width - Height / 6, (int)(Height * 0.15));
            Draw.Text(g, _item.Name, _nameFont, nameBounds, _palette.Text, _palette.Card, Draw.Center | TextFormatFlags.EndEllipsis);
            var detailBounds = new Rectangle(Height / 12, (int)(Height * 0.79), Width - Height / 6, (int)(Height * 0.11));
            Draw.Text(g, _item.Detail, _detailFont, detailBounds, _palette.Muted, _palette.Card, Draw.Center | TextFormatFlags.EndEllipsis);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _numberFont.Dispose();
                _nameFont.Dispose();
                _detailFont.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

/// <summary>A small on-screen display for keyboard shortcut changes.</summary>
internal sealed class Osd : OverlayForm
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1400 };
    private Ui? _ui;
    private string _glyph = Glyphs.Sun;
    private string _label = string.Empty;
    private int _value;
    private int _maximum = 100;

    public Osd()
    {
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Hide();
        };
    }

    public void Present(Palette palette, int dpi, string language, string glyph, string label, int value, int maximum)
    {
        if (_ui is null || _ui.P != palette || _ui.Dpi != dpi || _ui.Language != language)
        {
            _ui?.Dispose();
            _ui = new Ui(palette, dpi, language);
        }

        _glyph = glyph;
        _label = label;
        _value = value;
        _maximum = Math.Max(1, maximum);
        BackColor = palette.Card;

        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        var size = new Size(_ui.S(300), _ui.S(68));
        Bounds = new Rectangle(area.Left + (area.Width - size.Width) / 2, area.Bottom - size.Height - _ui.S(56), size.Width, size.Height);
        Opacity = 0.97;
        if (!Visible)
        {
            Show();
        }

        Invalidate();
        _timer.Stop();
        _timer.Start();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.RoundCorners(this);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_ui is null)
        {
            return;
        }

        var g = e.Graphics;
        var p = _ui.P;
        g.Clear(p.Card);
        using (var pen = new Pen(p.Border))
        {
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        Draw.Text(g, _glyph, _ui.IconFont(22), new Rectangle(_ui.S(16), 0, _ui.S(32), Height), p.Text, p.Card, Draw.Center);
        var left = _ui.S(62);
        var right = Width - _ui.S(20);
        Draw.Text(g, _label, _ui.Font(13), new Rectangle(left, _ui.S(12), right - left - _ui.S(60), _ui.S(20)), p.Muted, p.Card);
        Draw.Text(g, $"{_value}%", _ui.Font(14, semibold: true), new Rectangle(right - _ui.S(60), _ui.S(12), _ui.S(60), _ui.S(20)), p.Text, p.Card, Draw.Right);

        var bar = new RectangleF(left, _ui.S(42), right - left, _ui.S(4));
        Draw.FillRound(g, bar, bar.Height / 2, p.Track);
        var fill = bar with { Width = Math.Max(bar.Height, bar.Width * _value / _maximum) };
        Draw.FillRound(g, fill, bar.Height / 2, p.Accent);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _ui?.Dispose();
        }

        base.Dispose(disposing);
    }
}
