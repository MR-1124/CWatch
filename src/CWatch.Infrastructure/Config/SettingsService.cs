using System.Text.Json;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;

namespace CWatch.Infrastructure.Config;

public sealed class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly ILoggerService? _logger;
    private AppSettings _settings = new();

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
                    PropertyNameCaseInsensitive = true
                });
                if (loaded != null)
                {
                    _settings = loaded;
                    _logger?.LogInfo("Settings loaded successfully.");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to load settings file; using defaults.", ex);
        }

        _settings = new AppSettings();
        await SaveSettingsAsync();
    }

    public async Task SaveSettingsAsync()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(_settingsFilePath, json);
            _logger?.LogInfo("Settings saved successfully.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to save settings file.", ex);
        }
    }
}
