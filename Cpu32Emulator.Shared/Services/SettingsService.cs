using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Cpu32Emulator.Models;

namespace Cpu32Emulator.Services;

/// <summary>
/// Service for managing application settings and configuration
/// </summary>
public class SettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsFilePath;
    private AppConfig _currentConfig;

    public SettingsService(ILogger<SettingsService>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsService>.Instance;
        
        // Store settings in AppData/Local
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, "CPU32Emulator");
        Directory.CreateDirectory(appFolder);
        _settingsFilePath = Path.Combine(appFolder, "settings.json");
        
        _currentConfig = new AppConfig();
    }

    /// <summary>
    /// Gets the current application configuration
    /// </summary>
    public AppConfig CurrentConfig => _currentConfig;

    /// <summary>
    /// Loads settings from disk
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null)
                {
                    _currentConfig = config;
                    _logger.LogInformation("Settings loaded from {Path}", _settingsFilePath);
                }
            }
            else
            {
                _logger.LogInformation("No settings file found, using defaults");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings from {Path}", _settingsFilePath);
        }
    }

    /// <summary>
    /// Saves current settings to disk
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            var json = JsonSerializer.Serialize(_currentConfig, options);
            await File.WriteAllTextAsync(_settingsFilePath, json);
            _logger.LogInformation("Settings saved to {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to {Path}", _settingsFilePath);
        }
    }

    /// <summary>
    /// Updates the last project path and saves settings
    /// </summary>
    public async Task SetLastProjectPathAsync(string? projectPath)
    {
        _currentConfig = _currentConfig with { LastProjectPath = projectPath };
        await SaveAsync();
    }

    /// <summary>
    /// Gets the last project path
    /// </summary>
    public string? GetLastProjectPath()
    {
        return _currentConfig.LastProjectPath;
    }
}
