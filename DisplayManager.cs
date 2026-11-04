using System.Runtime.InteropServices;

namespace MonitorDimmer;

/// <summary>
/// One physical monitor. Hardware (DDC/CI) writes run on a background thread and are
/// coalesced, so dragging a slider never blocks the UI and only the latest value is sent.
/// </summary>
internal sealed class DisplayDevice : IDisposable
{
    private readonly object _gate = new();
    private readonly uint _minimum;
    private readonly uint _maximum;
    private IntPtr _handle;
    private int _pending = -1;
    private bool _running;
    private bool _disposed;

    internal DisplayDevice(string key, string gdiName, string model, Rectangle bounds, bool isPrimary, IntPtr handle, uint minimum, uint current, uint maximum, bool supportsHardware)
    {
        Key = key;
        GdiName = gdiName;
        Model = model;
        Bounds = bounds;
        IsPrimary = isPrimary;
        _handle = handle;
        _minimum = minimum;
        _maximum = maximum;
        SupportsHardware = supportsHardware && maximum > minimum;
        Brightness = SupportsHardware ? (int)Math.Round((current - minimum) * 100d / (maximum - minimum)) : 100;
    }

    public string Key { get; }
    public string GdiName { get; }
    public string Model { get; }
    public Rectangle Bounds { get; }
    public bool IsPrimary { get; }
    public bool SupportsHardware { get; }
    public int Number { get; internal set; }
    public int Brightness { get; private set; }
    public int ExtraDim { get; set; }
    public bool Linked { get; set; } = true;

    /// <summary>Raised on a worker thread when the monitor rejects a brightness write.</summary>
    internal Action<DisplayDevice>? WriteFailed { get; set; }

    public void RequestBrightness(int percent)
    {
        Brightness = Math.Clamp(percent, 0, 100);
        if (!SupportsHardware)
        {
            return;
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _pending = Brightness;
            if (_running)
            {
                return;
            }

            _running = true;
        }

        ThreadPool.QueueUserWorkItem(_ => Pump());
    }

    private void Pump()
    {
        while (true)
        {
            int value;
            IntPtr handle;
            lock (_gate)
            {
                if (_disposed || _pending < 0)
                {
                    _running = false;
                    if (_disposed)
                    {
                        DestroyHandle();
                    }
                    return;
                }

                value = _pending;
                _pending = -1;
                handle = _handle;
            }

            var target = (uint)Math.Round(_minimum + (_maximum - _minimum) * (value / 100d));
            var ok = Native.SetMonitorBrightness(handle, target);
            if (!ok)
            {
                Thread.Sleep(80);
                ok = Native.SetMonitorBrightness(handle, target);
            }

            if (!ok)
            {
                WriteFailed?.Invoke(this);
            }
        }
    }

    private void DestroyHandle()
    {
        if (_handle != IntPtr.Zero)
        {
            Native.DestroyPhysicalMonitor(_handle);
            _handle = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            if (!_running)
            {
                DestroyHandle();
            }
        }
    }
}

/// <summary>Discovers displays off the UI thread (DDC/CI reads can take hundreds of ms).</summary>
internal sealed class DisplayManager : IDisposable
{
    private readonly SynchronizationContext _ui;
    private List<DisplayDevice> _devices = [];
    private bool _discovering;
    private bool _refreshAgain;
    private bool _disposed;

    public DisplayManager()
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
    }

    public event Action? Changed;

    public IReadOnlyList<DisplayDevice> Devices => _devices;
    public bool HasDiscovered { get; private set; }
    public bool IsDiscovering => _discovering;

    public void Post(Action action) => _ui.Post(_ => action(), null);

    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        if (_discovering)
        {
            _refreshAgain = true;
            return;
        }

        _discovering = true;
        Task.Run(() =>
        {
            List<DisplayDevice> found;
            try
            {
                found = Discover();
            }
            catch (Exception)
            {
                found = [];
            }

            _ui.Post(_ => Complete(found), null);
        });
    }

    private void Complete(List<DisplayDevice> found)
    {
        _discovering = false;
        if (_disposed)
        {
            found.ForEach(device => device.Dispose());
            return;
        }

        var old = _devices;
        _devices = found;
        old.ForEach(device => device.Dispose());
        HasDiscovered = true;
        Changed?.Invoke();

        if (_refreshAgain)
        {
            _refreshAgain = false;
            Refresh();
        }
    }

    private static List<DisplayDevice> Discover()
    {
        var targets = Native.QueryDisplayTargets();
        var monitors = new List<IntPtr>();
        Native.MonitorEnumProc callback = (hMonitor, _, _, _) =>
        {
            monitors.Add(hMonitor);
            return true;
        };
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        var devices = new List<DisplayDevice>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hMonitor in monitors)
        {
            var info = new Native.MONITORINFOEX { cbSize = Marshal.SizeOf<Native.MONITORINFOEX>() };
            if (!Native.GetMonitorInfo(hMonitor, ref info))
            {
                continue;
            }

            var gdiName = string.IsNullOrWhiteSpace(info.szDevice) ? "Display" : info.szDevice;
            var bounds = info.rcMonitor.ToRectangle();
            var isPrimary = (info.dwFlags & 1) != 0;
            targets.TryGetValue(gdiName, out var targetList);

            Native.PHYSICAL_MONITOR[] physical = [];
            if (Native.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out var count) && count > 0)
            {
                physical = new Native.PHYSICAL_MONITOR[count];
                if (!Native.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physical))
                {
                    physical = [];
                }
            }

            if (physical.Length == 0)
            {
                var target = targetList?.FirstOrDefault();
                devices.Add(new DisplayDevice(UniqueKey(keys, KeyFor(target, gdiName, "", 0)), gdiName, ModelFor(target, ""), bounds, isPrimary, IntPtr.Zero, 0, 0, 0, false));
                continue;
            }

            for (var i = 0; i < physical.Length; i++)
            {
                var description = physical[i].szPhysicalMonitorDescription?.Trim() ?? string.Empty;
                var handle = physical[i].hPhysicalMonitor;
                var supported = TryReadBrightness(handle, out var minimum, out var current, out var maximum);
                if (!supported)
                {
                    Native.DestroyPhysicalMonitor(handle);
                    handle = IntPtr.Zero;
                }

                var target = targetList is not null && i < targetList.Count ? targetList[i] : null;
                devices.Add(new DisplayDevice(
                    UniqueKey(keys, KeyFor(target, gdiName, description, i)),
                    gdiName,
                    ModelFor(target, description),
                    bounds,
                    isPrimary,
                    handle,
                    minimum,
                    current,
                    maximum,
                    supported));
            }
        }

        var ordered = devices.OrderBy(device => device.Bounds.Left).ThenBy(device => device.Bounds.Top).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Number = i + 1;
        }

        return ordered;
    }

    private static bool TryReadBrightness(IntPtr handle, out uint minimum, out uint current, out uint maximum)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (Native.GetMonitorBrightness(handle, out minimum, out current, out maximum) && maximum > minimum)
            {
                return true;
            }

            Thread.Sleep(40);
        }

        minimum = current = maximum = 0;
        return false;
    }

    private static string KeyFor(TargetInfo? target, string gdiName, string description, int index) =>
        !string.IsNullOrWhiteSpace(target?.DevicePath) ? target.DevicePath : $"{gdiName}|{description}|{index}";

    private static string UniqueKey(HashSet<string> keys, string key)
    {
        var candidate = key;
        for (var i = 2; !keys.Add(candidate); i++)
        {
            candidate = $"{key}#{i}";
        }

        return candidate;
    }

    private static string ModelFor(TargetInfo? target, string description)
    {
        if (!string.IsNullOrWhiteSpace(target?.FriendlyName))
        {
            return target.FriendlyName.Trim();
        }

        if (target?.IsInternal == true)
        {
            return "Built-in display";
        }

        return string.IsNullOrWhiteSpace(description) ? "Display" : description;
    }

    public void Dispose()
    {
        _disposed = true;
        _devices.ForEach(device => device.Dispose());
        _devices = [];
    }
}
