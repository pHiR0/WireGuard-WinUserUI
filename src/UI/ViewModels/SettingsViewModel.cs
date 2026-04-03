using System;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WireGuard.UI.ViewModels;

/// <summary>
/// Ajustes de la aplicación UI. Se persisten en %AppData%\WireGuard-WinUserUI\settings.json.
/// Cualquier cambio en una propiedad se guarda automáticamente.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WireGuard-WinUserUI",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    // Flag to avoid saving during the initial Load()
    private bool _isLoaded;

    [ObservableProperty]
    private int _refreshIntervalSeconds = 5;

    [ObservableProperty]
    private bool _enableNotifications = true;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _startMinimized;

    public SettingsViewModel()
    {
        Load();
        _isLoaded = true;
    }

    // Auto-save whenever any property changes (after Load)
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_isLoaded)
            Save();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var data = new SettingsData
            {
                RefreshIntervalSeconds = RefreshIntervalSeconds,
                EnableNotifications = EnableNotifications,
                MinimizeToTray = MinimizeToTray,
                StartMinimized = StartMinimized,
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data, JsonOpts));
        }
        catch { /* silently ignore write errors */ }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = File.ReadAllText(SettingsPath);
            var data = JsonSerializer.Deserialize<SettingsData>(json, JsonOpts);
            if (data is null) return;

            RefreshIntervalSeconds = Math.Clamp(data.RefreshIntervalSeconds, 1, 300);
            EnableNotifications = data.EnableNotifications;
            MinimizeToTray = data.MinimizeToTray;
            StartMinimized = data.StartMinimized;
        }
        catch { /* use defaults */ }
    }

    private sealed class SettingsData
    {
        public int RefreshIntervalSeconds { get; set; } = 5;
        public bool EnableNotifications { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimized { get; set; }
    }
}

