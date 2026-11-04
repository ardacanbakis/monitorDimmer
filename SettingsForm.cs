namespace MonitorDimmer;

internal sealed record DisplayRowInfo(string Key, int Number, string Model, string? CustomName);

internal sealed class SettingsForm : Form
{
    private readonly Ui _ui;
    private readonly AppSettings _draft;
    private readonly IReadOnlyList<DisplayRowInfo> _displays;
    private readonly Action<string> _identify;
    private readonly ToolTip _tips = new();
    private readonly Dictionary<string, TextField> _nameFields = [];
    private RowsCard _presetsCard = null!;
    private AboutCard _aboutCard = null!;
    private VStack _stack = null!;
    private readonly bool _showAbout;

    public SettingsForm(AppSettings settings, IReadOnlyList<DisplayRowInfo> displays, Palette palette, string language, int dpi, Icon icon, Action<string> identify, bool showAbout = false)
    {
        _showAbout = showAbout;
        _draft = settings.Clone();
        _draft.StartWithWindows = StartupManager.IsEnabled();
        _displays = displays;
        _identify = identify;
        _ui = new Ui(palette, dpi, language);

        Text = T("settings");
        Icon = icon;
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        BackColor = palette.Window;
        ForeColor = palette.Text;
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Size = new Size(_ui.S(620), Math.Min(_ui.S(800), area.Height - _ui.S(60)));
        MinimumSize = new Size(_ui.S(520), _ui.S(460));
        Build();
    }

    public AppSettings Result => _draft;

    private string T(string key, params object[] values) => _ui.T(key, values);

    private void Build()
    {
        SuspendLayout();
        var stack = new VStack(_ui) { Dock = DockStyle.Fill, Padding = new Padding(_ui.S(24), 0, _ui.S(24), _ui.S(12)) };
        Controls.Add(stack);

        var header = new Surface(_ui) { Dock = DockStyle.Top, Height = _ui.S(60) };
        header.Painter = (surface, g) => Draw.Text(g, T("settings"), _ui.DisplayFont(24), new Rectangle(_ui.S(24), _ui.S(8), surface.Width - _ui.S(48), surface.Height - _ui.S(8)), _ui.P.Text, _ui.P.Window);
        Controls.Add(header);

        var footer = new Surface(_ui) { Dock = DockStyle.Bottom, Height = _ui.S(64) };
        var save = new IconButton(_ui, string.Empty, T("save"), ButtonKind.Primary);
        save.Width = Math.Max(save.Width, _ui.S(110));
        save.Click += (_, _) => SaveAndClose();
        var cancel = new IconButton(_ui, string.Empty, T("cancel"), ButtonKind.Standard);
        cancel.Width = Math.Max(cancel.Width, _ui.S(110));
        cancel.Click += (_, _) => Close();
        footer.Controls.AddRange([cancel, save]);
        footer.Arrange = surface =>
        {
            save.Location = new Point(surface.Width - _ui.S(24) - save.Width, (surface.Height - save.Height) / 2);
            cancel.Location = new Point(save.Left - _ui.S(8) - cancel.Width, save.Top);
        };
        footer.Painter = (surface, g) =>
        {
            using var pen = new Pen(_ui.P.Border);
            g.DrawLine(pen, 0, 0, surface.Width, 0);
        };
        Controls.Add(footer);

        stack.Controls.Add(new SectionLabel(_ui, T("general")));
        stack.Controls.Add(Rows(
            ToggleRow(T("startWithWindows"), T("startWithWindowsDesc"), _draft.StartWithWindows, value => _draft.StartWithWindows = value),
            ToggleRow(T("closeToTray"), T("closeToTrayDesc"), _draft.CloseToTray, value => _draft.CloseToTray = value),
            ToggleRow(T("hotkeys"), T("hotkeysDesc"), _draft.Hotkeys, value => _draft.Hotkeys = value)));

        stack.Controls.Add(new SectionLabel(_ui, T("appearance")));
        var theme = new Segmented(_ui, [T("deviceDefault"), T("light"), T("dark")], (int)_draft.Theme);
        theme.SelectedIndexChanged += (_, _) => _draft.Theme = (AppTheme)theme.SelectedIndex;
        stack.Controls.Add(Rows(new SettingRow(_ui, T("theme"), null, theme), new SettingRow(_ui, T("language"), null, CreateLanguagePicker())));

        stack.Controls.Add(new SectionLabel(_ui, T("displayNames")));
        stack.Controls.Add(Rows(_displays.Count == 0
            ? [MessageRow(T("noDisplays"))]
            : _displays.Select(NameRow).ToArray()));

        stack.Controls.Add(new SectionLabel(_ui, T("presets")));
        _presetsCard = new RowsCard(_ui);
        RebuildPresetRows();
        stack.Controls.Add(_presetsCard);

        stack.Controls.Add(new SectionLabel(_ui, T("about")));
        _aboutCard = new AboutCard(_ui, _tips);
        stack.Controls.Add(_aboutCard);
        _stack = stack;

        ResumeLayout(true);
    }

    private RowsCard Rows(params Control[] rows)
    {
        var card = new RowsCard(_ui);
        card.Controls.AddRange(rows);
        return card;
    }

    private SettingRow ToggleRow(string title, string description, bool value, Action<bool> changed)
    {
        var toggle = new ToggleSwitch(_ui, value, title);
        toggle.CheckedChanged += (_, _) => changed(toggle.Checked);
        return new SettingRow(_ui, title, description, toggle);
    }

    private ComboBox CreateLanguagePicker()
    {
        var picker = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = _ui.P.Input,
            ForeColor = _ui.P.Text,
            Font = _ui.Font(14),
            Width = _ui.S(240),
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = _ui.S(26)
        };
        var languages = new List<LanguageInfo> { new("system", $"{T("deviceDefault")} ({DevicePreferences.SystemLanguageName})") };
        languages.AddRange(Strings.Languages);
        foreach (var language in languages)
        {
            picker.Items.Add(language);
        }

        picker.SelectedItem = languages.FirstOrDefault(language => language.Code.Equals(_draft.Language, StringComparison.OrdinalIgnoreCase)) ?? languages[0];
        picker.DrawItem += (_, e) =>
        {
            if (e.Index < 0)
            {
                return;
            }

            var selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            var fill = selected ? _ui.P.AccentSoft : _ui.P.Input;
            using (var brush = new SolidBrush(fill))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            var text = ((LanguageInfo)picker.Items[e.Index]).NativeName;
            Draw.Text(e.Graphics, text, _ui.Font(14), new Rectangle(e.Bounds.X + _ui.S(6), e.Bounds.Y, e.Bounds.Width - _ui.S(8), e.Bounds.Height), _ui.P.Text, fill, Draw.Line);
        };
        picker.SelectedIndexChanged += (_, _) => _draft.Language = ((LanguageInfo)picker.SelectedItem!).Code;
        picker.HandleCreated += (_, _) =>
        {
            if (_ui.P.IsDark)
            {
                Native.SetWindowTheme(picker.Handle, "DarkMode_CFD", null);
            }
        };
        return picker;
    }

    private Surface MessageRow(string text)
    {
        var row = new Surface(_ui) { BackColor = _ui.P.Card, Height = _ui.S(52) };
        row.Painter = (surface, g) => Draw.Text(g, text, _ui.Font(13), surface.ClientRectangle, _ui.P.Muted, _ui.P.Card);
        return row;
    }

    private Surface NameRow(DisplayRowInfo display)
    {
        var row = new Surface(_ui) { BackColor = _ui.P.Card, Height = _ui.S(58) };
        var field = new TextField(_ui, display.CustomName ?? string.Empty, display.Model);
        _nameFields[display.Key] = field;
        var identify = new IconButton(_ui, Glyphs.Identify) { AccessibleName = T("identify") };
        identify.Click += (_, _) => _identify(display.Key);
        _tips.SetToolTip(identify, T("identify"));
        row.Controls.AddRange([field, identify]);
        row.Arrange = surface =>
        {
            identify.Location = new Point(surface.Width - identify.Width, (surface.Height - identify.Height) / 2);
            field.SetBounds(_ui.S(44), (surface.Height - field.Height) / 2, Math.Max(_ui.S(80), identify.Left - _ui.S(52)), field.Height);
        };
        row.Painter = (surface, g) => Badge.Paint(_ui, g, new Rectangle(0, (surface.Height - _ui.S(30)) / 2, _ui.S(30), _ui.S(30)), display.Number.ToString(), glyph: false);
        return row;
    }

    private void RebuildPresetRows()
    {
        _presetsCard.SuspendLayout();
        foreach (var control in _presetsCard.Controls.Cast<Control>().ToList())
        {
            _presetsCard.Controls.Remove(control);
            control.Dispose();
        }

        foreach (var preset in _draft.Presets)
        {
            _presetsCard.Controls.Add(PresetRow(preset));
        }

        var addRow = new Surface(_ui) { BackColor = _ui.P.Card, Height = _ui.S(58) };
        var add = new IconButton(_ui, Glyphs.Add, T("newPreset"), ButtonKind.Standard) { Enabled = _draft.Presets.Count < AppSettings.MaxPresets };
        add.Click += (_, _) =>
        {
            if (_draft.Presets.Count >= AppSettings.MaxPresets)
            {
                return;
            }

            _draft.Presets.Add(new Preset { Name = $"{T("newPreset")} {_draft.Presets.Count + 1}", Brightness = 60, Dim = 20 });
            RebuildPresetRows();
        };
        addRow.Controls.Add(add);
        addRow.Arrange = surface => add.Location = new Point(0, (surface.Height - add.Height) / 2);
        addRow.Painter = (surface, g) =>
        {
            var left = add.Right + _ui.S(12);
            Draw.Text(g, T("maxPresets"), _ui.Font(12.5f), new Rectangle(left, 0, surface.Width - left, surface.Height), _ui.P.Muted, _ui.P.Card);
        };
        _presetsCard.Controls.Add(addRow);
        _presetsCard.ResumeLayout(true);
    }

    private Surface PresetRow(Preset preset)
    {
        var row = new Surface(_ui) { BackColor = _ui.P.Card, Height = _ui.S(98) };
        var name = new TextField(_ui, preset.Name, T("name"));
        name.Box.TextChanged += (_, _) => preset.Name = name.Box.Text;
        var delete = new IconButton(_ui, Glyphs.Delete) { AccessibleName = T("delete") };
        _tips.SetToolTip(delete, T("delete"));
        delete.Click += (_, _) =>
        {
            _draft.Presets.Remove(preset);
            BeginInvoke(RebuildPresetRows);
        };
        var brightness = new ModernSlider(_ui, 0, 100, preset.Brightness) { AccessibleName = T("brightness") };
        brightness.ValueChanged += (_, _) =>
        {
            preset.Brightness = brightness.Value;
            row.Invalidate();
        };
        var dim = new ModernSlider(_ui, 0, 80, preset.Dim) { AccessibleName = T("extraDim") };
        dim.ValueChanged += (_, _) =>
        {
            preset.Dim = dim.Value;
            row.Invalidate();
        };
        row.Controls.AddRange([name, delete, brightness, dim]);

        Rectangle Half(int index)
        {
            var width = (row.Width - _ui.S(16)) / 2;
            return new Rectangle(index * (width + _ui.S(16)), _ui.S(54), width, _ui.S(32));
        }

        row.Arrange = surface =>
        {
            delete.Location = new Point(surface.Width - delete.Width, _ui.S(12));
            name.SetBounds(0, _ui.S(12), Math.Max(_ui.S(80), delete.Left - _ui.S(8)), name.Height);
            foreach (var (slider, index) in new[] { (brightness, 0), (dim, 1) })
            {
                var half = Half(index);
                slider.SetBounds(half.X + _ui.S(26), half.Y, Math.Max(_ui.S(40), half.Width - _ui.S(26) - _ui.S(46)), half.Height);
            }
        };
        row.Painter = (surface, g) =>
        {
            foreach (var (glyph, value, index) in new[] { (Glyphs.Sun, $"{preset.Brightness}%", 0), (Glyphs.Moon, SliderRow.DimText(_ui, preset.Dim), 1) })
            {
                var half = Half(index);
                Draw.Text(g, glyph, _ui.IconFont(15), new Rectangle(half.X, half.Y, _ui.S(22), half.Height), _ui.P.Text, _ui.P.Card, Draw.Center);
                Draw.Text(g, value, _ui.Font(13, semibold: true), new Rectangle(half.Right - _ui.S(44), half.Y, _ui.S(44), half.Height), _ui.P.Text, _ui.P.Card, Draw.Right);
            }
        };
        return row;
    }

    private void SaveAndClose()
    {
        foreach (var display in _displays)
        {
            if (!_nameFields.TryGetValue(display.Key, out var field))
            {
                continue;
            }

            var name = AppSettings.Clip(field.Box.Text);
            if (name is not null && name.Equals(display.Model, StringComparison.Ordinal))
            {
                name = null;
            }

            if (!_draft.Displays.TryGetValue(display.Key, out var prefs))
            {
                if (name is null)
                {
                    continue;
                }

                prefs = new DisplayPrefs();
                _draft.Displays[display.Key] = prefs;
            }

            prefs.Name = name;
        }

        _draft.Normalize();
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.ApplyWindowChrome(this, _ui.P);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_showAbout)
        {
            // About is the last section: scroll to the end so the whole card is visible.
            _stack.PerformLayout();
            _stack.AutoScrollPosition = new Point(0, _stack.DisplayRectangle.Height);
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _tips.Dispose();
            _ui.Dispose();
        }
    }
}

/// <summary>Title (and optional description) on the left, one control on the right.</summary>
internal sealed class SettingRow : Surface
{
    public SettingRow(Ui ui, string title, string? description, Control control) : base(ui)
    {
        BackColor = ui.P.Card;
        Height = ui.S(description is null ? 58 : 76);
        Controls.Add(control);
        Arrange = _ => control.Location = new Point(Width - control.Width, (Height - control.Height) / 2);
        Painter = (_, g) =>
        {
            var textWidth = Math.Max(ui.S(60), Width - control.Width - ui.S(20));
            if (description is null)
            {
                Draw.Text(g, title, ui.Font(14), new Rectangle(0, 0, textWidth, Height), ui.P.Text, ui.P.Card);
                return;
            }

            Draw.Text(g, title, ui.Font(14), new Rectangle(0, ui.S(12), textWidth, ui.S(20)), ui.P.Text, ui.P.Card);
            Draw.Text(g, description, ui.Font(12.5f), new Rectangle(0, ui.S(34), textWidth, ui.S(36)), ui.P.Muted, ui.P.Card, Draw.Wrap);
        };
    }
}
