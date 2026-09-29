using System.Text;
using System.IO;

namespace PatarikAIOS;

/// <summary>
/// Keeps operational failures visible without exposing tokens or API keys.
/// The desktop application and automation host can both write here.
/// </summary>
public static class RuntimeDiagnostics
{
    private static readonly string PathToLog = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PatarikAIOS", "runtime-diagnostics.log");

    public static void Log(string area, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PathToLog)!);
            File.AppendAllText(PathToLog,
                $"{DateTime.Now:O} [{area}] {exception.GetType().Name}: {exception.Message}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch { /* Diagnostics must never stop operations. */ }
    }
}
