using System;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;

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

    [ObservableProperty]
    private bool _startWithWindows;

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "WireGuard-WinUserUI";

    public SettingsViewModel()
    {
        Load();
        _isLoaded = true;
    }

    // Auto-save whenever any property changes (after Load)
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_isLoaded) return;

        if (e.PropertyName == nameof(StartWithWindows))
            ApplyStartWithWindows(StartWithWindows);
        else
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

        // StartWithWindows is read from the registry (authoritative source)
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            StartWithWindows = key?.GetValue(RunValueName) is not null;
        }
        catch { /* leave false */ }
    }

    private void ApplyStartWithWindows(bool enable)
    {
        try
        {
            if (enable)
            {
                var exe = Environment.ProcessPath ?? string.Empty;
                if (string.IsNullOrEmpty(exe)) return;
                using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
                key.SetValue(RunValueName, $"\"{exe}\"");
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                key?.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
        }
        catch { /* ignore */ }
    }

    private sealed class SettingsData
    {
        public int RefreshIntervalSeconds { get; set; } = 5;
        public bool EnableNotifications { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimized { get; set; }
    }
}

