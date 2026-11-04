namespace MonitorDimmer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--export-icon")
        {
            File.WriteAllBytes(args[1], Logo.BuildIco(Logo.IcoSizes));
            return 0;
        }

        if (args.Length == 2 && args[0] == "--export-png")
        {
            using var bitmap = Logo.Render(256);
            bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
            return 0;
        }

        if (args.Length >= 1 && args[0] == "--selftest")
        {
            var problems = Strings.Validate().ToList();
            if (args.Length == 2)
            {
                File.WriteAllLines(args[1], problems.Count == 0 ? ["OK"] : problems);
            }

            return problems.Count == 0 ? 0 : 1;
        }

        // One instance only: a second launch just brings the running one to the front.
        using var mutex = new Mutex(initiallyOwned: true, @"Local\MonitorDimmer.Claude.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            Native.PostMessage(Native.HwndBroadcast, Native.ShowMessage, IntPtr.Zero, IntPtr.Zero);
            return 0;
        }

        ApplicationConfiguration.Initialize();
        var startHidden = args.Any(arg => arg.Equals("--tray", StringComparison.OrdinalIgnoreCase));
        using var form = new MainForm();
        Application.Run(new TrayContext(form, startHidden));
        return 0;
    }

    /// <summary>Keeps the app alive while the window is hidden; ends only on Exit.</summary>
    private sealed class TrayContext : ApplicationContext
    {
        public TrayContext(MainForm form, bool startHidden)
        {
            form.ExitRequested += (_, _) => ExitThread();
            if (startHidden)
            {
                form.StartHidden();
            }
            else
            {
                form.Show();
            }
        }
    }
}
