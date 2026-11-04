using Microsoft.Win32;

namespace MonitorDimmer;

internal sealed class MainForm : Form
{
    private const int HotkeyUp = 1;
    private const int HotkeyDown = 2;

    private readonly SettingsStore _store = new();
    private readonly DisplayManager _displays = new();
    private readonly ScreenDimmer _dimmer = new();
    private readonly IdentifyOverlays _identify = new();
    private readonly Osd _osd = new();
    private readonly NotifyIcon _tray = new();
    private readonly ContextMenuStrip _menu = new();
    private readonly Font _menuFont = new("Segoe UI", 9.5f);
    private readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 700 };
    private readonly System.Windows.Forms.Timer _displayTimer = new() { Interval = 1500 };
    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 4500 };
    private readonly System.Windows.Forms.Timer _themeTimer = new() { Interval = 400 };
    private readonly List<MonitorCard> _cards = [];
    private readonly Icon _windowIcon;
    private readonly Icon _trayImage;

    private AppSettings _settings;
    private Ui _ui = null!;
    private ToolTip _tips = new();
    private Bitmap? _headerLogo;
    private VStack? _stack;
    private Surface? _statusBar;
    private MasterCard? _master;
    private PresetCard? _presetCard;
    private int _masterBrightness = 100;
    private int _masterDim;
    private string? _transientStatus;
    private bool _exiting;
    private bool _hotkeysRegistered;
    private DateTime _lastDeactivated;
    private DateTime _lastFailureNotice;

    public MainForm()
    {
        _settings = _store.Load();
        Text = "Monitor Dimmer";
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        _windowIcon = Logo.CreateIcon();
        Icon = _windowIcon;
        _trayImage = Logo.CreateIcon(SystemInformation.SmallIconSize);
        SetupTray();

        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            _store.Save(_settings);
        };
        _displayTimer.Tick += (_, _) =>
        {
            _displayTimer.Stop();
            _displays.Refresh();
        };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            _transientStatus = null;
            _statusBar?.Invalidate();
        };
        _themeTimer.Tick += (_, _) =>
        {
            _themeTimer.Stop();
            BuildUi();
        };
        _displays.Changed += OnDisplaysChanged;
        Deactivate += (_, _) => _lastDeactivated = DateTime.UtcNow;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        BuildUi();
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
        Size = new Size(_ui.S(560), Math.Min(_ui.S(840), area.Height - _ui.S(40)));
        MinimumSize = new Size(_ui.S(440), _ui.S(420));

        _displays.Refresh();
    }

    /// <summary>Raised once the app has cleaned up and the message loop should end.</summary>
    public event EventHandler? ExitRequested;

    private string T(string key, params object[] values) => _ui.T(key, values);

    public void StartHidden() => _ = Handle;

    // ------------------------------------------------------------------ tray

    private void SetupTray()
    {
        _menu.Opening += (_, _) => BuildTrayMenu();
        _tray.Icon = _trayImage;
        _tray.Text = "Monitor Dimmer";
        _tray.ContextMenuStrip = _menu;
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ToggleFromTray();
            }
        };
        _tray.BalloonTipClicked += (_, _) => ShowFromTray();
        _tray.Visible = true;
    }

    private void ToggleFromTray()
    {
        // Clicking the tray icon takes focus away from us first, so "was just active" counts as active.
        var wasActive = ActiveForm == this || (DateTime.UtcNow - _lastDeactivated).TotalMilliseconds < 500;
        if (Visible && WindowState != FormWindowState.Minimized && wasActive)
        {
            HideToTray();
        }
        else
        {
            ShowFromTray();
        }
    }

    public void ShowFromTray()
    {
        if (_exiting)
        {
            return;
        }

        if (!Visible)
        {
            Show();
        }

        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Activate();
        Native.SetForegroundWindow(Handle);
    }

    /// <summary>
    /// Hide() alone removes the window from the taskbar and Alt+Tab. The original toggled
    /// ShowInTaskbar here, which makes WinForms destroy and recreate the window handle in the
    /// middle of WM_CLOSE â€” that is what left the window visible.
    /// </summary>
    private void HideToTray()
    {
        Hide();
        if (!_settings.TrayHintShown)
        {
            _settings.TrayHintShown = true;
            ScheduleSave();
            _tray.ShowBalloonTip(4000, "Monitor Dimmer", T("trayHint"), ToolTipIcon.None);
        }
    }

    private void BuildTrayMenu()
    {
        _menu.SuspendLayout();
        foreach (ToolStripItem item in _menu.Items.Cast<ToolStripItem>().ToList())
        {
            item.Dispose();
        }

        _menu.Items.Clear();
        _menu.Font = _menuFont;
        _menu.Renderer = new MenuRenderer(_ui.P, _menuFont);
        _menu.ShowCheckMargin = true;
        _menu.ShowImageMargin = false;

        var open = new ToolStripMenuItem(T("open")) { Font = new Font(_menuFont, FontStyle.Bold) };
        open.Click += (_, _) => ShowFromTray();
        _menu.Items.Add(open);
        _menu.Items.Add(new ToolStripSeparator());

        foreach (var preset in _settings.Presets)
        {
            var item = new ToolStripMenuItem(preset.Name)
            {
                Checked = IsPresetActive(preset),
                ShortcutKeyDisplayString = $"{preset.Brightness}% Â· {SliderRow.DimText(_ui, preset.Dim)}"
            };
            item.Click += (_, _) => ApplyPreset(preset);
            _menu.Items.Add(item);
        }

        _menu.Items.Add(new ToolStripSeparator());
        var dimOff = new ToolStripMenuItem(T("dimOff")) { Enabled = _displays.Devices.Any(device => device.ExtraDim > 0) };
        dimOff.Click += (_, _) => TurnOffDim();
        _menu.Items.Add(dimOff);
        var identify = new ToolStripMenuItem(T("identify")) { Enabled = _displays.Devices.Count > 0 };
        identify.Click += (_, _) => Identify(null);
        _menu.Items.Add(identify);
        var settings = new ToolStripMenuItem(T("settings"));
        settings.Click += (_, _) => BeginInvoke(() =>
        {
            ShowFromTray();
            OpenSettings();
        });
        _menu.Items.Add(settings);
        var about = new ToolStripMenuItem(T("about"));
        about.Click += (_, _) => BeginInvoke(() =>
        {
            ShowFromTray();
            OpenSettings(showAbout: true);
        });
        _menu.Items.Add(about);
        _menu.Items.Add(new ToolStripSeparator());
        var exit = new ToolStripMenuItem(T("exit"));
        exit.Click += (_, _) => BeginInvoke(ExitApp);
        _menu.Items.Add(exit);
        _menu.ResumeLayout();
    }

    private void UpdateTrayText()
    {
        var targets = Targets();
        var text = "Monitor Dimmer";
        if (targets.Count > 0)
        {
            var hardware = targets.Where(device => device.SupportsHardware).ToList();
            var brightness = hardware.Count > 0 ? $"{T("brightness")} {_masterBrightness}%" : null;
            var dim = $"{T("extraDim")}: {SliderRow.DimText(_ui, _masterDim)}";
            text += "\n" + string.Join(" Â· ", new[] { brightness, dim }.Where(part => part is not null));
        }

        _tray.Text = text.Length > 127 ? text[..127] : text;
    }

    // -------------------------------------------------------------- building

    private void BuildUi()
    {
        SuspendLayout();
        var oldControls = Controls.Cast<Control>().ToList();
        Controls.Clear();
        oldControls.ForEach(control => control.Dispose());
        _cards.Clear();
        _master = null;
        _presetCard = null;
        _tips.Dispose();
        _tips = new ToolTip();
        _headerLogo?.Dispose();

        var language = DevicePreferences.ResolveLanguage(_settings.Language);
        var palette = Palette.Create(DevicePreferences.IsDark(_settings.Theme));
        _ui?.Dispose();
        _ui = new Ui(palette, DeviceDpi, language);
        _headerLogo = Logo.Render(_ui.S(28));
        BackColor = palette.Window;
        ForeColor = palette.Text;
        Native.ApplyWindowChrome(this, palette);

        _stack = new VStack(_ui) { Dock = DockStyle.Fill, Padding = new Padding(_ui.S(16), _ui.S(2), _ui.S(16), _ui.S(8)) };
        Controls.Add(_stack);
        Controls.Add(CreateHeader());
        _statusBar = new Surface(_ui) { Dock = DockStyle.Bottom, Height = _ui.S(34) };
        _statusBar.Painter = (bar, g) =>
        {
            var text = _transientStatus ?? BaseStatus();
            Draw.Text(g, text, _ui.Font(12.5f), new Rectangle(_ui.S(20), 0, bar.Width - _ui.S(40), bar.Height), _ui.P.Muted, _ui.P.Window);
        };
        Controls.Add(_statusBar);

        PopulateStack();
        ResumeLayout(true);
        UpdateTrayText();
    }

    private Control CreateHeader()
    {
        var header = new Surface(_ui) { Dock = DockStyle.Top, Height = _ui.S(64) };
        var settings = new IconButton(_ui, Glyphs.Settings) { AccessibleName = T("settings") };
        settings.Click += (_, _) => OpenSettings();
        _tips.SetToolTip(settings, T("settings"));
        var refresh = new IconButton(_ui, Glyphs.Refresh) { AccessibleName = T("refresh") };
        refresh.Click += (_, _) => _displays.Refresh();
        _tips.SetToolTip(refresh, T("refresh"));
        var identify = new IconButton(_ui, Glyphs.Identify) { AccessibleName = T("identify") };
        identify.Click += (_, _) => Identify(null);
        _tips.SetToolTip(identify, T("identify"));
        var support = new IconButton(_ui, Glyphs.Heart) { AccessibleName = T("support") };
        support.Click += (_, _) => OpenSettings(showAbout: true);
        _tips.SetToolTip(support, T("support"));
        header.Controls.AddRange([support, identify, refresh, settings]);

        header.Arrange = surface =>
        {
            var x = surface.Width - _ui.S(16);
            foreach (var button in new[] { settings, refresh, identify, support })
            {
                x -= button.Width;
                button.Location = new Point(x, (surface.Height - button.Height) / 2);
                x -= _ui.S(4);
            }
        };
        header.Painter = (surface, g) =>
        {
            if (_headerLogo is not null)
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(_headerLogo, _ui.S(20), (surface.Height - _headerLogo.Height) / 2);
            }

            Draw.Text(g, "Monitor Dimmer", _ui.DisplayFont(19), new Rectangle(_ui.S(58), 0, surface.Width - _ui.S(240), surface.Height), _ui.P.Text, _ui.P.Window);
        };
        return header;
    }

    private void PopulateStack()
    {
        if (_stack is null)
        {
            return;
        }

        _stack.SuspendLayout();
        foreach (var control in _stack.Controls.Cast<Control>().ToList())
        {
            _stack.Controls.Remove(control);
            control.Dispose();
        }

        _cards.Clear();
        _master = null;
        var devices = _displays.Devices;

        if (devices.Count == 0)
        {
            var message = new Card(_ui) { Height = _ui.S(96), Margin = new Padding(0, _ui.S(10), 0, _ui.S(8)) };
            var text = _displays.HasDiscovered ? T("noDisplays") : T("searching");
            message.Painter = (card, g) => Draw.Text(g, text, _ui.Font(14), card.ClientRectangle, _ui.P.Muted, _ui.P.Card, Draw.Center);
            _stack.Controls.Add(message);
        }
        else
        {
            if (devices.Count > 1)
            {
                var targets = Targets();
                _master = new MasterCard(_ui, _masterBrightness, _masterDim, targets.Any(device => device.SupportsHardware), targets.Count)
                {
                    Margin = new Padding(0, _ui.S(8), 0, _ui.S(8))
                };
                _master.BrightnessChanged += value =>
                {
                    _masterBrightness = value;
                    foreach (var device in Targets().Where(device => device.SupportsHardware))
                    {
                        device.RequestBrightness(value);
                    }

                    SyncViews(skipMaster: true);
                };
                _master.DimChanged += value =>
                {
                    _masterDim = value;
                    foreach (var device in Targets())
                    {
                        SetDim(device, value);
                    }

                    ApplyDim();
                    SyncViews(skipMaster: true);
                };
                _stack.Controls.Add(_master);
            }

            _stack.Controls.Add(new SectionLabel(_ui, T("displays")));
            foreach (var device in devices)
            {
                var card = new MonitorCard(_ui, device, DisplayName(device), showSync: devices.Count > 1, _tips);
                card.BrightnessChanged += value =>
                {
                    device.RequestBrightness(value);
                    RecomputeMaster();
                    SyncViews(skipCards: true);
                };
                card.DimChanged += value =>
                {
                    SetDim(device, value);
                    ApplyDim();
                    RecomputeMaster();
                    SyncViews(skipCards: true);
                };
                card.SyncChanged += linked =>
                {
                    device.Linked = linked;
                    Prefs(device).Linked = linked;
                    ScheduleSave();
                    RecomputeMaster();
                    PopulateStack();
                };
                card.Renamed += name =>
                {
                    Prefs(device).Name = name;
                    ScheduleSave();
                };
                card.IdentifyRequested += () => Identify(device);
                _cards.Add(card);
                _stack.Controls.Add(card);
            }
        }

        _stack.Controls.Add(new SectionLabel(_ui, T("presets")));
        _presetCard = new PresetCard(_ui, _settings.Presets, IsPresetActive);
        _presetCard.PresetClicked += ApplyPreset;
        _presetCard.SaveCurrentClicked += SaveCurrentAsPreset;
        _presetCard.EditClicked += OpenSettings;
        _stack.Controls.Add(_presetCard);
        _stack.ResumeLayout(true);
        _statusBar?.Invalidate();
    }

    // ----------------------------------------------------------------- state

    private void OnDisplaysChanged()
    {
        foreach (var device in _displays.Devices)
        {
            if (_settings.Displays.TryGetValue(device.Key, out var prefs))
            {
                device.ExtraDim = prefs.ExtraDim;
                device.Linked = prefs.Linked;
            }

            device.WriteFailed = failed => _displays.Post(() => OnWriteFailed(failed));
        }

        RecomputeMaster();
        ApplyDim(rebuild: true);
        PopulateStack();
        UpdateTrayText();
    }

    private void OnWriteFailed(DisplayDevice device)
    {
        if ((DateTime.UtcNow - _lastFailureNotice).TotalSeconds < 5)
        {
            return;
        }

        _lastFailureNotice = DateTime.UtcNow;
        ShowTransient(T("hardwareFailed", DisplayName(device)));
    }

    /// <summary>Displays that follow "All displays", presets and shortcuts.</summary>
    private List<DisplayDevice> Targets()
    {
        var devices = _displays.Devices;
        return devices.Count == 1 ? devices.ToList() : devices.Where(device => device.Linked).ToList();
    }

    private void RecomputeMaster()
    {
        var targets = Targets();
        var hardware = targets.Where(device => device.SupportsHardware).ToList();
        if (hardware.Count > 0)
        {
            _masterBrightness = (int)Math.Round(hardware.Average(device => device.Brightness));
        }

        _masterDim = targets.Count > 0 ? (int)Math.Round(targets.Average(device => device.ExtraDim)) : 0;
    }

    private void SyncViews(bool skipMaster = false, bool skipCards = false)
    {
        if (!skipCards)
        {
            _cards.ForEach(card => card.SyncFromDevice());
        }

        if (!skipMaster)
        {
            _master?.SetValues(_masterBrightness, _masterDim);
        }

        _presetCard?.RefreshActive();
        UpdateTrayText();
    }

    private DisplayPrefs Prefs(DisplayDevice device)
    {
        if (!_settings.Displays.TryGetValue(device.Key, out var prefs))
        {
            prefs = new DisplayPrefs { Linked = device.Linked };
            _settings.Displays[device.Key] = prefs;
        }

        return prefs;
    }

    private string DisplayName(DisplayDevice device) =>
        _settings.Displays.TryGetValue(device.Key, out var prefs) && !string.IsNullOrWhiteSpace(prefs.Name) ? prefs.Name : device.Model;

    private void SetDim(DisplayDevice device, int value)
    {
        device.ExtraDim = Math.Clamp(value, 0, 80);
        Prefs(device).ExtraDim = device.ExtraDim;
        ScheduleSave();
    }

    private void ApplyDim(bool rebuild = false) =>
        _dimmer.Apply(_displays.Devices.Select(device => (device.Bounds, device.ExtraDim)), rebuild);

    private bool IsPresetActive(Preset preset)
    {
        var targets = Targets();
        return targets.Count > 0
            && targets.All(device => device.ExtraDim == preset.Dim)
            && targets.Where(device => device.SupportsHardware).All(device => device.Brightness == preset.Brightness);
    }

    private void ApplyPreset(Preset preset)
    {
        var targets = Targets();
        foreach (var device in targets)
        {
            if (device.SupportsHardware)
            {
                device.RequestBrightness(preset.Brightness);
            }

            SetDim(device, preset.Dim);
        }

        ApplyDim();
        _masterBrightness = preset.Brightness;
        _masterDim = preset.Dim;
        SyncViews();
        ShowTransient(T("presetApplied", preset.Name));
    }

    private void SaveCurrentAsPreset()
    {
        if (_settings.Presets.Count >= AppSettings.MaxPresets)
        {
            ShowTransient(T("maxPresets"));
            return;
        }

        var number = _settings.Presets.Count + 1;
        var name = $"{T("newPreset")} {number}";
        while (_settings.Presets.Any(preset => preset.Name == name))
        {
            name = $"{T("newPreset")} {++number}";
        }

        _settings.Presets.Add(new Preset { Name = name, Brightness = _masterBrightness, Dim = _masterDim });
        ScheduleSave();
        PopulateStack();
        ShowTransient($"{name} Â· {_masterBrightness}% Â· {SliderRow.DimText(_ui, _masterDim)}");
    }

    private void TurnOffDim()
    {
        foreach (var device in _displays.Devices)
        {
            SetDim(device, 0);
        }

        ApplyDim();
        RecomputeMaster();
        SyncViews();
    }

    private void Step(int delta)
    {
        var targets = Targets();
        if (targets.Count == 0)
        {
            return;
        }

        var hardware = targets.Where(device => device.SupportsHardware).ToList();
        if (hardware.Count > 0)
        {
            _masterBrightness = Math.Clamp(_masterBrightness + delta, 0, 100);
            hardware.ForEach(device => device.RequestBrightness(_masterBrightness));
            SyncViews();
            _osd.Present(_ui.P, _ui.Dpi, _ui.Language, Glyphs.Sun, T("brightness"), _masterBrightness, 100);
        }
        else
        {
            _masterDim = Math.Clamp(_masterDim - delta, 0, 80);
            targets.ForEach(device => SetDim(device, _masterDim));
            ApplyDim();
            SyncViews();
            _osd.Present(_ui.P, _ui.Dpi, _ui.Language, Glyphs.Moon, T("extraDim"), _masterDim, 80);
        }
    }

    private void Identify(DisplayDevice? only)
    {
        var devices = only is null ? _displays.Devices.ToList() : [only];
        _identify.Show(devices.Select(device =>
        {
            var name = DisplayName(device);
            var detail = name == device.Model ? device.GdiName.Replace(@"\\.\", string.Empty) : device.Model;
            return new IdentifyItem(device.Bounds, device.Number, name, detail);
        }), _ui.P);
    }

    private void ShowTransient(string message)
    {
        _transientStatus = message;
        _statusBar?.Invalidate();
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    private string BaseStatus()
    {
        var devices = _displays.Devices;
        if (!_displays.HasDiscovered)
        {
            return T("searching");
        }

        return devices.Count == 0 ? T("noDisplays") : T("detected", devices.Count, devices.Count(device => device.SupportsHardware));
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    // -------------------------------------------------------------- settings

    private void OpenSettings() => OpenSettings(showAbout: false);

    private void OpenSettings(bool showAbout)
    {
        if (OwnedForms.OfType<SettingsForm>().FirstOrDefault() is { } open)
        {
            open.Activate();
            return;
        }

        var rows = _displays.Devices.Select(device => new DisplayRowInfo(
            device.Key,
            device.Number,
            device.Model,
            _settings.Displays.TryGetValue(device.Key, out var prefs) ? prefs.Name : null)).ToList();

        using var form = new SettingsForm(_settings, rows, _ui.P, _ui.Language, DeviceDpi, _windowIcon, key =>
        {
            if (_displays.Devices.FirstOrDefault(device => device.Key == key) is { } device)
            {
                Identify(device);
            }
        }, showAbout);

        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settings = form.Result;
        string? error = null;
        if (_settings.StartWithWindows != StartupManager.IsEnabled())
        {
            error = StartupManager.SetEnabled(_settings.StartWithWindows);
            if (error is not null)
            {
                _settings.StartWithWindows = false;
            }
        }

        _store.Save(_settings);
        UpdateHotkeys();
        BuildUi();
        if (error is not null)
        {
            ShowTransient(T(error));
        }
    }

    // ------------------------------------------------------- window plumbing

    private void UpdateHotkeys()
    {
        if (_hotkeysRegistered && IsHandleCreated)
        {
            Native.UnregisterHotKey(Handle, HotkeyUp);
            Native.UnregisterHotKey(Handle, HotkeyDown);
        }

        _hotkeysRegistered = false;
        if (!_settings.Hotkeys || !IsHandleCreated || _exiting)
        {
            return;
        }

        var up = Native.RegisterHotKey(Handle, HotkeyUp, Native.ModControl | Native.ModAlt, Native.VkPrior);
        var down = Native.RegisterHotKey(Handle, HotkeyDown, Native.ModControl | Native.ModAlt, Native.VkNext);
        _hotkeysRegistered = up || down;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.ApplyWindowChrome(this, _ui.P);
        UpdateHotkeys();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (_hotkeysRegistered)
        {
            Native.UnregisterHotKey(Handle, HotkeyUp);
            Native.UnregisterHotKey(Handle, HotkeyDown);
            _hotkeysRegistered = false;
        }

        base.OnHandleDestroyed(e);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        BuildUi();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.ShowMessage)
        {
            ShowFromTray();
            return;
        }

        switch (m.Msg)
        {
            case Native.WmHotkey:
                Step(m.WParam.ToInt32() == HotkeyUp ? 10 : -10);
                return;
            case Native.WmDisplayChange:
                _displayTimer.Stop();
                _displayTimer.Start();
                break;
            case Native.WmSettingChange:
                if (m.LParam != IntPtr.Zero && System.Runtime.InteropServices.Marshal.PtrToStringUni(m.LParam) == "ImmersiveColorSet")
                {
                    _themeTimer.Stop();
                    _themeTimer.Start();
                }
                break;
        }

        base.WndProc(ref m);
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume && IsHandleCreated)
        {
            BeginInvoke(() =>
            {
                _displayTimer.Stop();
                _displayTimer.Start();
            });
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exiting && e.CloseReason is CloseReason.UserClosing or CloseReason.None && _settings.CloseToTray)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        ExitApp();
    }

    private void ExitApp()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _saveTimer.Stop();
        _store.Save(_settings);
        _tray.Visible = false;
        _dimmer.Dispose();
        _identify.Dispose();
        _displays.Dispose();
        UpdateHotkeys();
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Dispose();
            _menu.Dispose();
            _menuFont.Dispose();
            _osd.Dispose();
            _saveTimer.Dispose();
            _displayTimer.Dispose();
            _statusTimer.Dispose();
            _themeTimer.Dispose();
            _tips.Dispose();
            _headerLogo?.Dispose();
            _windowIcon.Dispose();
            _trayImage.Dispose();
        }

        base.Dispose(disposing);
        if (disposing)
        {
            _ui?.Dispose();
        }
    }
}

/// <summary>Themed tray menu.</summary>
internal sealed class MenuRenderer : ToolStripProfessionalRenderer
{
    private readonly Palette _palette;

    public MenuRenderer(Palette palette, Font font) : base(new MenuColors(palette))
    {
        _palette = palette;
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? _palette.Text : _palette.Muted;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        using var font = new Font(FontFamilies.Icons, e.ImageRectangle.Height * 0.8f, GraphicsUnit.Pixel);
        TextRenderer.DrawText(e.Graphics, Glyphs.Check, font, e.ImageRectangle, _palette.Accent, Draw.Center);
    }

    private sealed class MenuColors(Palette p) : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => p.Card;
        public override Color ImageMarginGradientBegin => p.Card;
        public override Color ImageMarginGradientMiddle => p.Card;
        public override Color ImageMarginGradientEnd => p.Card;
        public override Color MenuBorder => p.Border;
        public override Color MenuItemBorder => p.Subtle;
        public override Color MenuItemSelected => p.Subtle;
        public override Color MenuItemSelectedGradientBegin => p.Subtle;
        public override Color MenuItemSelectedGradientEnd => p.Subtle;
        public override Color MenuItemPressedGradientBegin => p.Pressed;
        public override Color MenuItemPressedGradientEnd => p.Pressed;
        public override Color SeparatorDark => p.Border;
        public override Color SeparatorLight => p.Card;
        public override Color CheckBackground => p.Card;
        public override Color CheckSelectedBackground => p.Subtle;
        public override Color CheckPressedBackground => p.Pressed;
        public override Color ButtonSelectedBorder => p.Subtle;
    }
}
