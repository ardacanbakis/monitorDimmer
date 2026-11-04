namespace MonitorDimmer;

/// <summary>Shared geometry for a "glyph · slider · value" row inside a card.</summary>
internal static class SliderRow
{
    public static void Place(Ui ui, Control card, ModernSlider slider, int y)
    {
        var left = ui.S(52);
        slider.SetBounds(left, y, Math.Max(ui.S(60), card.Width - left - ui.S(16) - ui.S(56)), ui.S(32));
    }

    public static void Paint(Ui ui, Graphics g, Control card, string glyph, string value, int y, bool enabled = true)
    {
        var p = ui.P;
        Draw.Text(g, glyph, ui.IconFont(16), new Rectangle(ui.S(16), y, ui.S(28), ui.S(32)), enabled ? p.Text : p.Muted, p.Card, Draw.Center);
        Draw.Text(g, value, ui.Font(14, semibold: true), new Rectangle(card.Width - ui.S(16) - ui.S(52), y, ui.S(52), ui.S(32)), enabled ? p.Text : p.Muted, p.Card, Draw.Right);
    }

    public static string DimText(Ui ui, int value) => value == 0 ? ui.T("off") : $"{value}%";
}

internal static class Badge
{
    public static void Paint(Ui ui, Graphics g, Rectangle bounds, string content, bool glyph)
    {
        Draw.FillRound(g, bounds, ui.S(7), ui.P.AccentSoft);
        var font = glyph ? ui.IconFont(15) : ui.DisplayFont(15);
        Draw.Text(g, content, font, bounds, ui.P.Accent, ui.P.AccentSoft, Draw.Center);
    }
}

/// <summary>"All displays": moves every synced display together.</summary>
internal sealed class MasterCard : Card
{
    private readonly ModernSlider? _brightness;
    private readonly ModernSlider _dim;
    private readonly int _syncedCount;

    public MasterCard(Ui ui, int brightness, int dim, bool hasHardware, int syncedCount) : base(ui)
    {
        _syncedCount = syncedCount;
        if (hasHardware)
        {
            _brightness = new ModernSlider(ui, 0, 100, brightness) { AccessibleName = ui.T("brightness") };
            _brightness.ValueChanged += (_, _) =>
            {
                Invalidate();
                BrightnessChanged?.Invoke(_brightness.Value);
            };
            Controls.Add(_brightness);
        }

        _dim = new ModernSlider(ui, 0, 80, dim) { AccessibleName = ui.T("extraDim") };
        _dim.ValueChanged += (_, _) =>
        {
            Invalidate();
            DimChanged?.Invoke(_dim.Value);
        };
        _dim.ValueCommitted += (_, _) => DimCommitted?.Invoke();
        Controls.Add(_dim);

        Height = ui.S(hasHardware ? 150 : 112);
        Arrange = _ =>
        {
            var y = ui.S(66);
            if (_brightness is not null)
            {
                SliderRow.Place(ui, this, _brightness, y);
                y += ui.S(38);
            }

            SliderRow.Place(ui, this, _dim, y);
        };
        Painter = (_, g) => PaintContent(g);
    }

    public event Action<int>? BrightnessChanged;
    public event Action<int>? DimChanged;
    public event Action? DimCommitted;

    public void SetValues(int brightness, int dim)
    {
        _brightness?.SetValue(brightness);
        _dim.SetValue(dim);
        Invalidate();
    }

    private void PaintContent(Graphics g)
    {
        var p = Ui.P;
        Badge.Paint(Ui, g, new Rectangle(Ui.S(16), Ui.S(16), Ui.S(32), Ui.S(32)), Glyphs.Identify, glyph: true);
        Draw.Text(g, Ui.T("allDisplays"), Ui.Font(15, semibold: true), new Rectangle(Ui.S(60), Ui.S(13), Width - Ui.S(76), Ui.S(22)), p.Text, p.Card);
        Draw.Text(g, $"{Ui.T("allDisplaysHint")} · {_syncedCount}", Ui.Font(12.5f), new Rectangle(Ui.S(60), Ui.S(35), Width - Ui.S(76), Ui.S(18)), p.Muted, p.Card);

        var y = Ui.S(66);
        if (_brightness is not null)
        {
            SliderRow.Paint(Ui, g, this, Glyphs.Sun, $"{_brightness.Value}%", y);
            y += Ui.S(38);
        }

        SliderRow.Paint(Ui, g, this, Glyphs.Moon, SliderRow.DimText(Ui, _dim.Value), y);
    }
}

/// <summary>One monitor: inline rename, identify, sync toggle, brightness and extra dim.</summary>
internal sealed class MonitorCard : Card
{
    private readonly DisplayDevice _device;
    private readonly ModernSlider? _brightness;
    private readonly ModernSlider _dim;
    private readonly IconButton _rename;
    private readonly IconButton _identify;
    private readonly IconButton? _sync;
    private TextField? _editor;
    private string _name;
    private Rectangle _nameBounds;

    public MonitorCard(Ui ui, DisplayDevice device, string name, bool showSync, ToolTip tips) : base(ui)
    {
        _device = device;
        _name = name;
        Height = ui.S(152);

        if (device.SupportsHardware)
        {
            _brightness = new ModernSlider(ui, 0, 100, device.Brightness) { AccessibleName = $"{name} {ui.T("brightness")}" };
            _brightness.ValueChanged += (_, _) =>
            {
                Invalidate();
                BrightnessChanged?.Invoke(_brightness.Value);
            };
            Controls.Add(_brightness);
        }

        _dim = new ModernSlider(ui, 0, 80, device.ExtraDim) { AccessibleName = $"{name} {ui.T("extraDim")}" };
        _dim.ValueChanged += (_, _) =>
        {
            Invalidate();
            DimChanged?.Invoke(_dim.Value);
        };
        _dim.ValueCommitted += (_, _) => DimCommitted?.Invoke();
        Controls.Add(_dim);

        _rename = new IconButton(ui, Glyphs.Edit) { AccessibleName = ui.T("rename") };
        _rename.Size = new Size(ui.S(28), ui.S(28));
        _rename.Click += (_, _) => BeginRename();
        tips.SetToolTip(_rename, ui.T("rename"));
        Controls.Add(_rename);

        _identify = new IconButton(ui, Glyphs.Identify) { AccessibleName = ui.T("identify") };
        _identify.Click += (_, _) => IdentifyRequested?.Invoke();
        tips.SetToolTip(_identify, ui.T("identify"));
        Controls.Add(_identify);

        if (showSync)
        {
            _sync = new IconButton(ui, Glyphs.Link) { IsToggle = true, Checked = device.Linked, AccessibleName = ui.T("sync") };
            _sync.CheckedChanged += (_, _) => SyncChanged?.Invoke(_sync.Checked);
            tips.SetToolTip(_sync, ui.T("sync"));
            Controls.Add(_sync);
        }

        Arrange = _ => ArrangeChildren();
        Painter = (_, g) => PaintContent(g);
    }

    public DisplayDevice Device => _device;

    public event Action<int>? BrightnessChanged;
    public event Action<int>? DimChanged;
    public event Action? DimCommitted;
    public event Action<bool>? SyncChanged;
    public event Action<string?>? Renamed;
    public event Action? IdentifyRequested;

    public void SyncFromDevice()
    {
        _brightness?.SetValue(_device.Brightness);
        _dim.SetValue(_device.ExtraDim);
        if (_sync is not null)
        {
            _sync.Checked = _device.Linked;
        }

        Invalidate();
    }

    private int ButtonsLeft => (_sync?.Left ?? Width - Ui.S(16)) - (_identify.Width + Ui.S(4));

    private void ArrangeChildren()
    {
        var right = Width - Ui.S(16);
        if (_sync is not null)
        {
            _sync.Location = new Point(right - _sync.Width, Ui.S(14));
            right = _sync.Left - Ui.S(4);
        }

        _identify.Location = new Point(right - _identify.Width, Ui.S(14));

        var nameFont = Ui.Font(15, semibold: true);
        var maxName = Math.Max(Ui.S(40), _identify.Left - Ui.S(8) - Ui.S(60) - _rename.Width - Ui.S(4));
        var nameWidth = Math.Min(maxName, Draw.Measure(_name, nameFont) + Ui.S(2));
        _nameBounds = new Rectangle(Ui.S(60), Ui.S(13), nameWidth, Ui.S(22));
        _rename.Location = new Point(_nameBounds.Right + Ui.S(4), Ui.S(10));

        _editor?.SetBounds(Ui.S(56), Ui.S(10), Math.Max(Ui.S(120), Math.Min(Ui.S(300), _identify.Left - Ui.S(64))), Ui.S(32));

        if (_brightness is not null)
        {
            SliderRow.Place(Ui, this, _brightness, Ui.S(68));
        }

        SliderRow.Place(Ui, this, _dim, Ui.S(106));
    }

    private void PaintContent(Graphics g)
    {
        var p = Ui.P;
        Badge.Paint(Ui, g, new Rectangle(Ui.S(16), Ui.S(16), Ui.S(32), Ui.S(32)), _device.Number.ToString(), glyph: false);

        var subtitleBounds = new Rectangle(Ui.S(60), Ui.S(37), Math.Max(10, ButtonsLeft - Ui.S(60)), Ui.S(18));
        if (_editor is null)
        {
            Draw.Text(g, _name, Ui.Font(15, semibold: true), _nameBounds, p.Text, p.Card);
            var parts = new List<string>();
            if (!_name.Equals(_device.Model, StringComparison.Ordinal))
            {
                parts.Add(_device.Model);
            }

            parts.Add(_device.SupportsHardware ? Ui.T("hardwareControl") : Ui.T("softwareOnly"));
            if (_device.IsPrimary)
            {
                parts.Add(Ui.T("primary"));
            }

            Draw.Text(g, string.Join(" · ", parts), Ui.Font(12.5f), subtitleBounds, p.Muted, p.Card);
        }
        else
        {
            Draw.Text(g, Ui.T("renameHint"), Ui.Font(12), new Rectangle(Ui.S(60), Ui.S(44), Width - Ui.S(76), Ui.S(18)), p.Muted, p.Card);
        }

        if (_brightness is not null)
        {
            SliderRow.Paint(Ui, g, this, Glyphs.Sun, $"{_brightness.Value}%", Ui.S(68));
        }
        else
        {
            Draw.Text(g, Glyphs.Sun, Ui.IconFont(16), new Rectangle(Ui.S(16), Ui.S(68), Ui.S(28), Ui.S(32)), p.Muted, p.Card, Draw.Center);
            Draw.Text(g, Ui.T("ddcHint"), Ui.Font(12.5f), new Rectangle(Ui.S(52), Ui.S(68), Width - Ui.S(68), Ui.S(34)), p.Muted, p.Card,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        SliderRow.Paint(Ui, g, this, Glyphs.Moon, SliderRow.DimText(Ui, _dim.Value), Ui.S(106));
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (_nameBounds.Contains(e.Location))
        {
            BeginRename();
        }
    }

    public void BeginRename()
    {
        if (_editor is not null)
        {
            return;
        }

        _editor = new TextField(Ui, _name, _device.Model);
        _editor.Box.KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Enter or Keys.Escape)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                EndRename(commit: e.KeyCode == Keys.Enter);
            }
        };
        _editor.Box.Leave += (_, _) => EndRename(commit: true);
        Controls.Add(_editor);
        _rename.Visible = false;
        ArrangeChildren();
        Invalidate();
        _editor.Box.Focus();
        _editor.Box.SelectAll();
    }

    private void EndRename(bool commit)
    {
        if (_editor is not { } editor)
        {
            return;
        }

        _editor = null;
        if (commit)
        {
            var text = AppSettings.Clip(editor.Box.Text);
            var custom = text is null || text.Equals(_device.Model, StringComparison.Ordinal) ? null : text;
            _name = custom ?? _device.Model;
            Renamed?.Invoke(custom);
        }

        _rename.Visible = true;
        ArrangeChildren();
        Invalidate();
        BeginInvoke(() =>
        {
            Controls.Remove(editor);
            editor.Dispose();
        });
    }
}

/// <summary>One-click presets in a responsive grid, plus "Save current" and "Edit".</summary>
internal sealed class PresetCard : Card
{
    private readonly List<(Chip Chip, Preset? Preset)> _chips = [];
    private readonly Func<Preset, bool> _isActive;

    public PresetCard(Ui ui, IReadOnlyList<Preset> presets, Func<Preset, bool> isActive) : base(ui)
    {
        _isActive = isActive;
        foreach (var preset in presets)
        {
            var chip = new Chip(ui, preset.Name, preset.Brightness, preset.Dim) { Active = isActive(preset) };
            chip.Click += (_, _) => PresetClicked?.Invoke(preset);
            _chips.Add((chip, preset));
        }

        if (presets.Count < AppSettings.MaxPresets)
        {
            var save = new Chip(ui, ui.T("saveCurrent"), null, null, Glyphs.Add, dashed: true);
            save.Click += (_, _) => SaveCurrentClicked?.Invoke();
            _chips.Add((save, null));
        }

        var edit = new Chip(ui, ui.T("edit"), null, null, Glyphs.Edit, dashed: true);
        edit.Click += (_, _) => EditClicked?.Invoke();
        _chips.Add((edit, null));

        foreach (var (chip, _) in _chips)
        {
            Controls.Add(chip);
        }

        Height = ui.S(90);
        Arrange = _ => ArrangeChips();
    }

    public event Action<Preset>? PresetClicked;
    public event Action? SaveCurrentClicked;
    public event Action? EditClicked;

    public void RefreshActive()
    {
        foreach (var (chip, preset) in _chips)
        {
            if (preset is not null)
            {
                chip.Active = _isActive(preset);
            }
        }
    }

    private void ArrangeChips()
    {
        var pad = Ui.S(12);
        var gap = Ui.S(8);
        var available = Width - pad * 2;
        var columns = Math.Max(1, (available + gap) / (Ui.S(136) + gap));
        var width = (available - gap * (columns - 1)) / columns;
        for (var i = 0; i < _chips.Count; i++)
        {
            var chip = _chips[i].Chip;
            var column = i % columns;
            var row = i / columns;
            chip.SetBounds(pad + column * (width + gap), pad + row * (chip.Height + gap), width, chip.Height);
        }

        var rows = (_chips.Count + columns - 1) / columns;
        var height = pad * 2 + rows * Ui.S(58) + (rows - 1) * gap;
        if (Height != height)
        {
            Height = height;
        }
    }
}
