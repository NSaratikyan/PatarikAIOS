using System.Diagnostics;
using System.Text;

// Patarik AI OS Automation Host
// Keeps the background WPF scheduler alive independently of the visible window.

var appPath = GetOption(args, "--app")
    ?? Environment.GetEnvironmentVariable("PATARIK_APP_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "PatarikAIOS.exe");

if (!File.Exists(appPath))
{
    Console.Error.WriteLine($"Patarik AI OS executable was not found: {appPath}");
    return 2;
}

var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatarikAIOS", "automation");
Directory.CreateDirectory(dataDirectory);
var logPath = Path.Combine(dataDirectory, "automation.log");
var heartbeatPath = Path.Combine(dataDirectory, "heartbeat.txt");
var stopAfterChildExit = args.Any(x => string.Equals(x, "--once", StringComparison.OrdinalIgnoreCase));

WriteLog($"Automation Host started. App: {appPath}");
while (true)
{
    try
    {
        File.WriteAllText(heartbeatPath, $"Starting: {DateTime.Now:O}");
        using var child = Process.Start(new ProcessStartInfo
        {
            FileName = appPath,
            Arguments = "--automation",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(appPath)!
        });

        if (child is null) throw new InvalidOperationException("The Automation process could not be started.");
        WriteLog($"Background scheduler started (PID {child.Id}).");
        while (!child.HasExited)
        {
            File.WriteAllText(heartbeatPath, $"Running: {DateTime.Now:O}{Environment.NewLine}PID: {child.Id}");
            await Task.Delay(TimeSpan.FromMinutes(1));
        }
        WriteLog($"Background scheduler exited with code {child.ExitCode}.");
        if (stopAfterChildExit) return child.ExitCode;
    }
    catch (Exception ex)
    {
        WriteLog($"Automation error: {ex.GetType().Name}: {ex.Message}");
        if (stopAfterChildExit) return 1;
    }

    // Prevent a tight crash loop while still restoring automation quickly.
    await Task.Delay(TimeSpan.FromSeconds(15));
}

static string? GetOption(IEnumerable<string> args, string name)
{
    var values = args.ToArray();
    for (var i = 0; i < values.Length - 1; i++)
        if (string.Equals(values[i], name, StringComparison.OrdinalIgnoreCase)) return values[i + 1].Trim('"');
    return null;
}

static void WriteLog(string text)
{
    var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatarikAIOS", "automation", "automation.log");
    File.AppendAllText(path, $"{DateTime.Now:O}  {text}{Environment.NewLine}", Encoding.UTF8);
}
