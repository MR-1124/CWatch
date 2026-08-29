using System.Text;
using CWatch.Core.Interfaces;

namespace CWatch.Infrastructure.Logging;

public sealed class FileLoggerService : ILoggerService, IDisposable
{
    private readonly string _logDirectory;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private string? _currentLogDate;

    public FileLoggerService(string? customLogDir = null)
    {
        _logDirectory = customLogDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CWatch", "Logs");

        try
        {
            Directory.CreateDirectory(_logDirectory);
            CleanOldLogs(14);
            EnsureWriter();
        }
        catch
        {
            // Graceful fallback if directory cannot be created
        }
    }

    private void EnsureWriter()
    {
        lock (_lock)
        {
            string today = DateTime.UtcNow.ToString("yyyyMMdd");
            if (_writer != null && _currentLogDate == today)
            {
                return;
            }

            _writer?.Dispose();
            _currentLogDate = today;
            string filePath = Path.Combine(_logDirectory, $"cwatch_{today}.log");
            var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _writer = new StreamWriter(fs, Encoding.UTF8) { AutoFlush = true };
        }
    }

    public void LogInfo(string message) => WriteEntry("INFO", message);
    public void LogWarning(string message) => WriteEntry("WARN", message);
    public void LogError(string message, Exception? ex = null)
    {
        string fullMessage = ex != null ? $"{message} | Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}" : message;
        WriteEntry("ERROR", fullMessage);
    }

    private void WriteEntry(string level, string message)
    {
        try
        {
            EnsureWriter();
            string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
            lock (_lock)
            {
                _writer?.WriteLine(line);
            }
#if DEBUG
            System.Diagnostics.Debug.WriteLine(line);
#endif
        }
        catch
        {
            // Logging must never crash the application
        }
    }

    private void CleanOldLogs(int daysToKeep)
    {
        try
        {
            if (!Directory.Exists(_logDirectory)) return;
            var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);
            foreach (var file in Directory.GetFiles(_logDirectory, "cwatch_*.log"))
            {
                var fi = new FileInfo(file);
                if (fi.CreationTimeUtc < cutoff)
                {
                    try { fi.Delete(); } catch { }
                }
            }
        }
        catch
        {
            // Ignore cleanup failure
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
