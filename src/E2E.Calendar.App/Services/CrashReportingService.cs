using System.Text;
using System.Text.RegularExpressions;

namespace E2E.Calendar.App.Services;

public sealed class CrashReportingService
{
    private const long MaxLogBytes = 1024 * 1024;
    private static readonly Regex SecretPattern = new(
        @"(?i)(password|client_secret|access_token|refresh_token|authorization)\s*[:=]\s*[^\s,;]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _path;
    private readonly object _gate = new();

    public CrashReportingService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "E2E.Calendar",
            "logs");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "crashes.log");
    }

    public void Report(Exception exception, string context)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var text = $"{DateTimeOffset.UtcNow:O} [{context}] {exception.GetType().Name}: {exception.Message}{Environment.NewLine}{exception.StackTrace}{Environment.NewLine}";
        var safeText = SecretPattern.Replace(text, "$1=<redacted>");
        lock (_gate)
        {
            if (File.Exists(_path) && new FileInfo(_path).Length + Encoding.UTF8.GetByteCount(safeText) > MaxLogBytes)
            {
                File.Move(_path, $"{_path}.previous", true);
            }

            File.AppendAllText(_path, safeText, Encoding.UTF8);
        }
    }
}
