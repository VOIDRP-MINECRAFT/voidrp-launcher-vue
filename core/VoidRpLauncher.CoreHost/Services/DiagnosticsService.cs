using System.Text;

namespace VoidRpLauncher.CoreHost.Services;

public sealed class DiagnosticsService
{
    private readonly object _lock = new();
    private readonly Queue<string> _lines = new();

    public string Text
    {
        get
        {
            lock (_lock)
            {
                return string.Join(Environment.NewLine, _lines);
            }
        }
    }

    public void Info(string category, string message) => Append("INFO", category, message);
    public void Warn(string category, string message) => Append("WARN", category, message);
    public void Error(string category, string message) => Append("ERROR", category, message);
    public void AppendException(string category, Exception ex) => Append("ERROR", category, ex.ToString());

    public void Clear()
    {
        lock (_lock)
        {
            _lines.Clear();
        }
    }

    private void Append(string level, string category, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var normalized = message.Replace("\r\n", "\n");

        lock (_lock)
        {
            foreach (var line in normalized.Split('\n'))
            {
                _lines.Enqueue($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [{category}] {line}");
                while (_lines.Count > 4000)
                {
                    _lines.Dequeue();
                }
            }
        }
    }
}



