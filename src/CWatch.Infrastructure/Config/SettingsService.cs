using System.Text.Json;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;

namespace CWatch.Infrastructure.Config;

public sealed class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly ILoggerService? _logger;
    private AppSettings _settings = new();

    /// <summary>True when the stored file was corrupt or unreadable and defaults were substituted.</summary>
    public bool LastLoadHadProblems { get; private set; }

    public AppSettings Settings => _settings;

    public SettingsService(ILoggerService? logger = null, string? customFilePath = null)
    {
        _logger = logger;
        _settingsFilePath = customFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CWatch", "settings.json");
    }

    public async Task LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = await File.ReadAllTextAsync(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
                if (loaded != null)
                {
                    Sanitize(loaded);
                    _settings = loaded;
                    _logger?.LogInfo("Settings loaded successfully.");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            // Quarantine the corrupt file so the user's failed state is diagnosable
            // and defaults load cleanly, instead of failing on every launch.
            _logger?.LogError("Settings file unreadable; using defaults.", ex);
            LastLoadHadProblems = true;
            try
            {
                string backup = _settingsFilePath + ".corrupt";
                File.Copy(_settingsFilePath, backup, overwrite: true);
                File.Delete(_settingsFilePath);
                _logger?.LogWarning($"Corrupt settings file moved to {backup}.");
            }
            catch (Exception qEx)
            {
                _logger?.LogWarning($"Could not quarantine corrupt settings file: {qEx.Message}");
            }
        }

        _settings = new AppSettings();
        await SaveSettingsAsync();
    }

    public async Task SaveSettingsAsync()
    {
        try
        {
            Sanitize(_settings);

            string? dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // Atomic write: a crash mid-save can no longer leave a truncated file.
            string tempPath = _settingsFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, _settingsFilePath, overwrite: true);
            _logger?.LogInfo("Settings saved successfully.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to save settings file.", ex);
            throw;
        }
    }

    /// <summary>
    /// Corrects impossible persisted values so a hand-edited or partially written
    /// file can never produce nonsense configuration.
    /// </summary>
    private static void Sanitize(AppSettings s)
    {
        s.TargetDriveLetter = DriveLetters.Normalize(s.TargetDriveLetter);
        s.MonitorIntervalMinutes = Math.Clamp(s.MonitorIntervalMinutes, 1, 1440);
        s.RetentionDays = Math.Clamp(s.RetentionDays, 1, 3650);
        s.WarningThresholdGb = Math.Max(1, s.WarningThresholdGb);
        s.CriticalThresholdGb = Math.Max(1, s.CriticalThresholdGb);

        if (s.AppTheme is not ("Dark" or "Light" or "System"))
        {
            s.AppTheme = "Dark";
        }
    }
}
