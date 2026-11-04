using System.Drawing.Drawing2D;

namespace MonitorDimmer;

internal enum ButtonKind
{
    Ghost,
    Standard,
    Primary
}

/// <summary>A double-buffered panel whose layout and painting are supplied as delegates.</summary>
internal class Surface : Panel
{
    public Surface(Ui ui)
    {
        Ui = ui;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = ui.P.Window;
    }

    public Ui Ui { get; }
    public Action<Surface>? Arrange { get; set; }
    public Action<Surface, Graphics>? Painter { get; set; }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        Arrange?.Invoke(this);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Painter?.Invoke(this, e.Graphics);
    }
}

/// <summary>A rounded Windows 11-style card.</summary>
internal class Card : Surface
{
    public Card(Ui ui) : base(ui)
    {
        BackColor = ui.P.Card;
        Margin = new Padding(0, 0, 0, ui.S(8));
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Ui.P.Window);
        Draw.FillRound(e.Graphics, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Ui.S(8), Ui.P.Card, Ui.P.Border);
    }
}

/// <summary>A card whose children are stacked rows separated by hairlines.</summary>
internal sealed class RowsCard : Card
{
    public RowsCard(Ui ui) : base(ui)
    {
        Arrange = _ => Stack();
        Painter = (_, g) =>
        {
            using var pen = new Pen(Ui.P.Border);
            var rows = Controls.Cast<Control>().Where(control => control.Visible).ToList();
            for (var i = 0; i < rows.Count - 1; i++)
            {
                g.DrawLine(pen, Ui.S(16), rows[i].Bottom, Width - Ui.S(16), rows[i].Bottom);
            }
        };
    }

    private void Stack()
    {
        var y = Ui.S(2);
        foreach (Control row in Controls)
        {
            if (!row.Visible)
            {
                continue;
            }

            row.SetBounds(Ui.S(16), y, Math.Max(10, Width - Ui.S(32)), row.Height);
            y += row.Height;
        }

        var height = y + Ui.S(2);
        if (Height != height)
        {
            Height = height;
        }
    }
}

/// <summary>Vertical, full-width stack with dark scrollbars in dark mode.</summary>
internal sealed class VStack : FlowLayoutPanel
{
    private readonly bool _dark;

    public VStack(Ui ui)
    {
        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;
        BackColor = ui.P.Window;
        DoubleBuffered = true;
        _dark = ui.P.IsDark;
        AutoScroll = false;
        HorizontalScroll.Maximum = 0;
        HorizontalScroll.Visible = false;
        AutoScroll = true;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_dark)
        {
            Native.SetWindowTheme(Handle, "DarkMode_Explorer", null);
        }
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        var width = ClientSize.Width - Padding.Horizontal;
        foreach (Control control in Controls)
        {
            var target = Math.Max(40, width - control.Margin.Horizontal);
            if (control.Width != target)
            {
                control.Width = target;
            }
        }

        base.OnLayout(levent);
    }
}

internal sealed class SectionLabel : Control
{
    private readonly Ui _ui;

    public SectionLabel(Ui ui, string text)
    {
        _ui = ui;
        Text = text;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = ui.P.Window;
        Height = ui.S(38);
        Margin = new Padding(ui.S(2), 0, 0, 0);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var bounds = new Rectangle(0, 0, Width, Height - _ui.S(8));
        TextRenderer.DrawText(e.Graphics, Text, _ui.Font(14, semibold: true), bounds, _ui.P.Text, BackColor,
            TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }
}

/// <summary>Base for focusable, custom-painted controls.</summary>
internal abstract class SurfaceControl : Control
{
    protected SurfaceControl(Ui ui)
    {
        Ui = ui;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    protected Ui Ui { get; }
    protected bool IsHovered { get; private set; }
    protected bool IsPressed { get; private set; }
    protected Color SurfaceColor => Parent?.BackColor ?? Ui.P.Window;
    protected bool ShowFocus => Focused && ShowFocusCues;

    protected override void OnPaintBackground(PaintEventArgs pevent) => pevent.Graphics.Clear(SurfaceColor);

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        IsHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        IsHovered = false;
        IsPressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            IsPressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        IsPressed = false;
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected void DrawFocusRing(Graphics g, RectangleF bounds, float radius)
    {
        var mode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Draw.Round(bounds, radius);
        using var pen = new Pen(Ui.P.Text, Math.Max(1.5f, Ui.Scale * 1.5f));
        g.DrawPath(pen, path);
        g.SmoothingMode = mode;
    }
}

internal sealed class IconButton : SurfaceControl
{
    private bool _checked;

    public IconButton(Ui ui, string glyph, string text = "", ButtonKind kind = ButtonKind.Ghost) : base(ui)
    {
        Glyph = glyph;
        Text = text;
        Kind = kind;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = text;
        Fit();
    }

    public string Glyph { get; set; }
    public ButtonKind Kind { get; }
    public bool IsToggle { get; set; }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked != value)
            {
                _checked = value;
                Invalidate();
            }
        }
    }

    public event EventHandler? CheckedChanged;

    public void Fit()
    {
        if (string.IsNullOrEmpty(Text))
        {
            Size = new Size(Ui.S(34), Ui.S(34));
            return;
        }

        var glyphWidth = string.IsNullOrEmpty(Glyph) ? 0 : Ui.S(16) + Ui.S(8);
        Size = new Size(Ui.S(14) + glyphWidth + Draw.Measure(Text, TextFont) + Ui.S(14), Ui.S(34));
    }

    private Font TextFont => Ui.Font(14, semibold: Kind == ButtonKind.Primary);

    protected override void OnClick(EventArgs e)
    {
        if (IsToggle)
        {
            Checked = !Checked;
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }

        base.OnClick(e);
    }

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
        var g = e.Graphics;
        var p = Ui.P;
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        var radius = Ui.S(6);
        Color? fill = null;
        Color? border = null;
        var foreground = Enabled ? p.Text : p.Muted;

        switch (Kind)
        {
            case ButtonKind.Primary:
                fill = !Enabled ? p.Subtle : IsPressed ? Draw.Blend(p.Accent, SurfaceColor, 0.22f) : IsHovered ? p.AccentHover : p.Accent;
                foreground = Enabled ? p.OnAccent : p.Muted;
                break;
            case ButtonKind.Standard:
                fill = IsPressed ? p.Pressed : IsHovered ? p.Subtle : p.Input;
                border = p.InputBorder;
                break;
            default:
                fill = IsPressed ? p.Pressed : IsHovered ? p.Subtle : Checked ? p.AccentSoft : null;
                if (Checked && Enabled)
                {
                    foreground = p.Accent;
                }
                break;
        }

        if (fill is { } color)
        {
            Draw.FillRound(g, bounds, radius, color, border);
        }

        var background = fill ?? SurfaceColor;
        if (string.IsNullOrEmpty(Text))
        {
            Draw.Text(g, Glyph, Ui.IconFont(16), ClientRectangle, foreground, background, Draw.Center);
        }
        else
        {
            var x = Ui.S(14);
            if (!string.IsNullOrEmpty(Glyph))
            {
                Draw.Text(g, Glyph, Ui.IconFont(14), new Rectangle(x, 0, Ui.S(16), Height), foreground, background, Draw.Center);
                x += Ui.S(16) + Ui.S(8);
            }

            Draw.Text(g, Text, TextFont, new Rectangle(x, 0, Width - x - Ui.S(10), Height), foreground, background);
        }

        if (ShowFocus)
        {
            DrawFocusRing(g, new RectangleF(1, 1, Width - 3, Height - 3), radius);
        }
    }
}

/// <summary>A Windows 11-style slider. ValueChanged fires only for user input.</summary>
internal sealed class ModernSlider : SurfaceControl
{
    private readonly int _minimum;
    private readonly int _maximum;
    private int _value;
    private bool _dragging;

    public ModernSlider(Ui ui, int minimum, int maximum, int value) : base(ui)
    {
        _minimum = minimum;
        _maximum = Math.Max(minimum + 1, maximum);
        _value = Math.Clamp(value, _minimum, _maximum);
        Height = ui.S(32);
        AccessibleRole = AccessibleRole.Slider;
        AccessibleDescription = $"{_value}";
    }

    public int Value => _value;

    public event EventHandler? ValueChanged;
    public event EventHandler? ValueCommitted;

    public void SetValue(int value)
    {
        value = Math.Clamp(value, _minimum, _maximum);
        if (value == _value)
        {
            return;
        }

        _value = value;
        AccessibleDescription = $"{_value}";
        Invalidate();
    }

    private void UserSet(int value)
    {
        value = Math.Clamp(value, _minimum, _maximum);
        if (value == _value)
        {
            return;
        }

        SetValue(value);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private int ThumbDiameter => Ui.S(20);
    private int Inset => ThumbDiameter / 2 + 1;

    private int ValueFromX(int x)
    {
        var ratio = Math.Clamp((x - Inset) / (double)Math.Max(1, Width - Inset * 2), 0d, 1d);
        return _minimum + (int)Math.Round((_maximum - _minimum) * ratio);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var p = Ui.P;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var centerY = Height / 2f;
        var left = (float)Inset;
        var right = Width - Inset;
        var ratio = (_value - _minimum) / (float)(_maximum - _minimum);
        var x = left + (right - left) * ratio;
        var trackHeight = Ui.S(4);
        var fillColor = Enabled ? p.Accent : p.Track;

        using (var trackPath = Draw.Round(new RectangleF(left, centerY - trackHeight / 2f, right - left, trackHeight), trackHeight / 2f))
        using (var track = new SolidBrush(p.Track))
        {
            g.FillPath(track, trackPath);
        }

        if (x - left > 0.5f)
        {
            using var fillPath = Draw.Round(new RectangleF(left, centerY - trackHeight / 2f, x - left, trackHeight), trackHeight / 2f);
            using var fill = new SolidBrush(fillColor);
            g.FillPath(fill, fillPath);
        }

        var outer = ThumbDiameter / 2f;
        using (var thumb = new SolidBrush(p.ThumbOuter))
        using (var ring = new Pen(p.Border))
        {
            g.FillEllipse(thumb, x - outer, centerY - outer, outer * 2, outer * 2);
            g.DrawEllipse(ring, x - outer, centerY - outer, outer * 2, outer * 2);
        }

        var inner = Ui.Scale * (!Enabled ? 5 : _dragging || IsPressed ? 5 : IsHovered ? 7 : 6);
        using (var dot = new SolidBrush(fillColor))
        {
            g.FillEllipse(dot, x - inner, centerY - inner, inner * 2, inner * 2);
        }

        if (ShowFocus)
        {
            using var focus = new Pen(p.Text, Math.Max(1.5f, Ui.Scale * 1.5f));
            g.DrawEllipse(focus, x - outer - 2, centerY - outer - 2, outer * 2 + 4, outer * 2 + 4);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled)
        {
            return;
        }

        Focus();
        _dragging = true;
        Capture = true;
        UserSet(ValueFromX(e.X));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            UserSet(ValueFromX(e.X));
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragging)
        {
            _dragging = false;
            Capture = false;
            ValueCommitted?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        // Only a focused slider reacts, so scrolling the window never changes brightness by accident.
        if (!Focused || !Enabled)
        {
            base.OnMouseWheel(e);
            return;
        }

        if (e is HandledMouseEventArgs handled)
        {
            handled.Handled = true;
        }

        UserSet(_value + Math.Sign(e.Delta) * 5);
        ValueCommitted?.Invoke(this, EventArgs.Empty);
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int? next = e.KeyCode switch
        {
            Keys.Left or Keys.Down => _value - (e.Control ? 10 : 1),
            Keys.Right or Keys.Up => _value + (e.Control ? 10 : 1),
            Keys.PageDown => _value - 10,
            Keys.PageUp => _value + 10,
            Keys.Home => _minimum,
            Keys.End => _maximum,
            _ => null
        };

        if (next is { } value)
        {
            e.Handled = true;
            UserSet(value);
            ValueCommitted?.Invoke(this, EventArgs.Empty);
        }
    }
}

internal sealed class ToggleSwitch : SurfaceControl
{
    private bool _checked;

    public ToggleSwitch(Ui ui, bool isOn, string accessibleName) : base(ui)
    {
        _checked = isOn;
        Size = new Size(ui.S(40), ui.S(20));
        AccessibleRole = AccessibleRole.CheckButton;
        AccessibleName = accessibleName;
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked != value)
            {
                _checked = value;
                Invalidate();
            }
        }
    }

    public event EventHandler? CheckedChanged;

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
        CheckedChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Space)
        {
            e.Handled = true;
            OnClick(EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var p = Ui.P;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        var radius = bounds.Height / 2f;
        var knob = Ui.Scale * (IsPressed ? 14 : IsHovered ? 14 : 12);
        var centerY = Height / 2f;

        if (_checked)
        {
            Draw.FillRound(g, bounds, radius, IsHovered ? p.AccentHover : p.Accent);
            using var brush = new SolidBrush(p.OnAccent);
            var cx = Width - Height / 2f;
            g.FillEllipse(brush, cx - knob / 2, centerY - knob / 2, knob, knob);
        }
        else
        {
            Draw.FillRound(g, bounds, radius, IsHovered ? p.Subtle : SurfaceColor, p.Muted);
            using var brush = new SolidBrush(p.Muted);
            var cx = Height / 2f;
            g.FillEllipse(brush, cx - knob / 2, centerY - knob / 2, knob, knob);
        }

        if (ShowFocus)
        {
            DrawFocusRing(g, new RectangleF(1, 1, Width - 3, Height - 3), radius);
        }
    }
}

internal sealed class Segmented : SurfaceControl
{
    private readonly string[] _items;
    private readonly int[] _widths;
    private int _selected;
    private int _hover = -1;

    public Segmented(Ui ui, string[] items, int selected) : base(ui)
    {
        _items = items;
        _selected = Math.Clamp(selected, 0, items.Length - 1);
        _widths = items.Select(item => Draw.Measure(item, ui.Font(13)) + ui.S(24)).ToArray();
        Size = new Size(_widths.Sum() + ui.S(8), ui.S(34));
        AccessibleRole = AccessibleRole.PageTabList;
    }

    public int SelectedIndex => _selected;

    public event EventHandler? SelectedIndexChanged;

    private Rectangle Segment(int index)
    {
        var x = Ui.S(4) + _widths.Take(index).Sum();
        return new Rectangle(x, Ui.S(4), _widths[index], Height - Ui.S(8));
    }

    private int HitTest(Point point)
    {
        for (var i = 0; i < _items.Length; i++)
        {
            if (Segment(i).Contains(point))
            {
                return i;
            }
        }

        return -1;
    }

    private void Select(int index)
    {
        if (index < 0 || index >= _items.Length || index == _selected)
        {
            return;
        }

        _selected = index;
        Invalidate();
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hover = HitTest(e.Location);
        if (hover != _hover)
        {
            _hover = hover;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = -1;
        base.OnMouseLeave(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        Select(HitTest(e.Location));
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Left)
        {
            Select(_selected - 1);
        }
        else if (e.KeyCode == Keys.Right)
        {
            Select(_selected + 1);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var p = Ui.P;
        Draw.FillRound(g, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Ui.S(6), p.Input, p.InputBorder);
        for (var i = 0; i < _items.Length; i++)
        {
            var segment = Segment(i);
            var selected = i == _selected;
            var fill = selected ? p.Accent : i == _hover ? p.Subtle : p.Input;
            if (selected || i == _hover)
            {
                Draw.FillRound(g, segment, Ui.S(4), fill);
            }

            Draw.Text(g, _items[i], Ui.Font(13, semibold: selected), segment, selected ? p.OnAccent : p.Text, fill, Draw.Center);
        }

        if (ShowFocus)
        {
            DrawFocusRing(g, new RectangleF(1, 1, Width - 3, Height - 3), Ui.S(6));
        }
    }
}

/// <summary>A themed single-line text field with a Windows 11 focus underline.</summary>
internal sealed class TextField : Surface
{
    public TextField(Ui ui, string text, string placeholder) : base(ui)
    {
        BackColor = ui.P.Input;
        Height = ui.S(34);
        Box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = ui.P.Input,
            ForeColor = ui.P.Text,
            Font = ui.Font(14),
            Text = text,
            PlaceholderText = placeholder,
            MaxLength = AppSettings.MaxNameLength
        };
        Box.GotFocus += (_, _) => Invalidate();
        Box.LostFocus += (_, _) => Invalidate();
        Controls.Add(Box);
        Cursor = Cursors.IBeam;
        Click += (_, _) => Box.Focus();
        Arrange = _ => Box.SetBounds(Ui.S(10), (Height - Box.PreferredHeight) / 2, Math.Max(10, Width - Ui.S(20)), Box.PreferredHeight);
    }

    public TextBox Box { get; }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Ui.P.Card);
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Draw.FillRound(g, bounds, Ui.S(5), Ui.P.Input, Ui.P.InputBorder);
        if (Box.Focused)
        {
            using var path = Draw.Round(bounds, Ui.S(5));
            g.SetClip(path);
            using var accent = new SolidBrush(Ui.P.Accent);
            g.FillRectangle(accent, 0, Height - Ui.S(2), Width, Ui.S(2));
            g.ResetClip();
        }
    }
}

/// <summary>A preset tile: name plus brightness and dim values.</summary>
internal sealed class Chip : SurfaceControl
{
    private readonly string _title;
    private readonly int? _brightness;
    private readonly int? _dim;
    private readonly string? _glyph;
    private readonly bool _dashed;
    private bool _active;

    public Chip(Ui ui, string title, int? brightness, int? dim, string? glyph = null, bool dashed = false) : base(ui)
    {
        _title = title;
        _brightness = brightness;
        _dim = dim;
        _glyph = glyph;
        _dashed = dashed;
        Height = ui.S(58);
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = title;
    }

    public bool Active
    {
        get => _active;
        set
        {
            if (_active != value)
            {
                _active = value;
                Invalidate();
            }
        }
    }

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
        var g = e.Graphics;
        var p = Ui.P;
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        var radius = Ui.S(6);
        var fill = IsPressed ? p.Pressed : IsHovered ? p.Subtle : _active ? p.AccentSoft : _dashed ? SurfaceColor : p.Layer;

        if (_dashed)
        {
            Draw.FillRound(g, bounds, radius, fill);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Draw.Round(bounds, radius);
            using var pen = new Pen(p.InputBorder) { DashStyle = DashStyle.Dash };
            g.DrawPath(pen, path);
            var glyphWidth = Ui.S(16) + Ui.S(8);
            var total = glyphWidth + Draw.Measure(_title, Ui.Font(13, semibold: true));
            var x = (Width - total) / 2;
            Draw.Text(g, _glyph ?? "", Ui.IconFont(14), new Rectangle(x, 0, Ui.S(16), Height), p.Muted, fill, Draw.Center);
            Draw.Text(g, _title, Ui.Font(13, semibold: true), new Rectangle(x + glyphWidth, 0, Width - x - glyphWidth - Ui.S(6), Height), p.Text, fill);
        }
        else
        {
            Draw.FillRound(g, bounds, radius, fill, _active ? p.Accent : p.Border);
            var pad = Ui.S(14);
            Draw.Text(g, _title, Ui.Font(14, semibold: true), new Rectangle(pad, Ui.S(9), Width - pad * 2, Ui.S(20)), p.Text, fill);
            var x = pad;
            var y = new Rectangle(0, Ui.S(31), 0, Ui.S(18));
            void Value(string glyph, string text)
            {
                Draw.Text(g, glyph, Ui.IconFont(11), new Rectangle(x, y.Y, Ui.S(13), y.Height), p.Muted, fill, Draw.Center);
                x += Ui.S(17);
                var width = Draw.Measure(text, Ui.Font(12));
                Draw.Text(g, text, Ui.Font(12), new Rectangle(x, y.Y, width + Ui.S(2), y.Height), p.Muted, fill);
                x += width + Ui.S(12);
            }

            Value(Glyphs.Sun, $"{_brightness}%");
            Value(Glyphs.Moon, _dim == 0 ? Ui.T("off") : $"{_dim}%");
        }

        if (_active && !_dashed)
        {
            Draw.Text(g, Glyphs.Check, Ui.IconFont(12), new Rectangle(Width - Ui.S(28), Ui.S(9), Ui.S(16), Ui.S(20)), p.Accent, fill, Draw.Center);
        }

        if (ShowFocus)
        {
            DrawFocusRing(g, new RectangleF(1, 1, Width - 3, Height - 3), radius);
        }
    }
}
