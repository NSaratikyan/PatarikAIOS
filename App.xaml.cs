namespace PatarikAIOS;

public partial class App : Application
{
    public static AppServices Services { get; } = new(new DemoDataProvider());
    private Mutex? _automationMutex;

    /// <summary>
    /// Starts the same business logic without displaying the WPF window.  This is
    /// used only by the Automation Host; a normal user launch still shows the app.
    /// </summary>
    public bool IsAutomationMode { get; private set; }

    public bool TryAcquireTelegramAutomationLease()
    {
        if (_automationMutex is not null) return true;
        var candidate = new Mutex(initiallyOwned: true, @"Local\PatarikAIOS.TelegramAutomation", out var createdNew);
        if (!createdNew)
        {
            candidate.Dispose();
            return false;
        }
        _automationMutex = candidate;
        return true;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        IsAutomationMode = e.Args.Any(x => string.Equals(x, "--automation", StringComparison.OrdinalIgnoreCase));
        ShutdownMode = IsAutomationMode ? ShutdownMode.OnExplicitShutdown : ShutdownMode.OnMainWindowClose;

        var window = new MainWindow();
        MainWindow = window;
        if (IsAutomationMode)
        {
            // Showing once triggers WPF's Loaded event and the scheduler, then the
            // window is removed from the taskbar. The process remains independent
            // from the owner's visible application window.
            window.ShowInTaskbar = false;
            window.WindowState = WindowState.Minimized;
            window.Show();
            window.Hide();
            return;
        }

        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_automationMutex is not null)
        {
            try { _automationMutex.ReleaseMutex(); } catch (ApplicationException) { }
            _automationMutex.Dispose();
        }
        base.OnExit(e);
    }
}
