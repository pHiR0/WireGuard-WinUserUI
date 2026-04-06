using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using WireGuard.UI.Services;

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
    private bool _enableNotifications = true;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private bool _startWithWindows;

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "WireGuard-WinUserUI";

    public ObservableCollection<PublicIpProviderViewModel> PublicIpProviders { get; } = [];

    [ObservableProperty]
    private PublicIpProviderViewModel? _selectedPrimaryProvider;

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
        else if (e.PropertyName == nameof(SelectedPrimaryProvider))
        {
            foreach (var p in PublicIpProviders)
                p.IsPrimary = p == SelectedPrimaryProvider;
            Save();
        }
        else
            Save();
    }

    /// <summary>Returns the ordered list of enabled provider IDs: primary first, then enabled fallbacks.</summary>
    public IReadOnlyList<string> GetOrderedEnabledProviderIds()
    {
        var result = new List<string>();
        if (SelectedPrimaryProvider is not null)
            result.Add(SelectedPrimaryProvider.Id);
        result.AddRange(PublicIpProviders
            .Where(p => p != SelectedPrimaryProvider && p.IsEnabled)
            .Select(p => p.Id));
        return result;
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var data = new SettingsData
            {
                EnableNotifications = EnableNotifications,
                MinimizeToTray = MinimizeToTray,
                StartMinimized = StartMinimized,
                PrimaryProviderId = SelectedPrimaryProvider?.Id ?? "ipify",
                EnabledFallbackIds = PublicIpProviders
                    .Where(p => p != SelectedPrimaryProvider && p.IsEnabled)
                    .Select(p => p.Id)
                    .ToList(),
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data, JsonOpts));
        }
        catch { /* silently ignore write errors */ }
    }

    private void Load()
    {
        SettingsData? data = null;
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                data = JsonSerializer.Deserialize<SettingsData>(json, JsonOpts);
            }
        }
        catch { /* use defaults */ }

        if (data is not null)
        {
            EnableNotifications = data.EnableNotifications;
            MinimizeToTray = data.MinimizeToTray;
            StartMinimized = data.StartMinimized;
        }

        // Build provider VMs from the static provider list
        var primaryId = data?.PrimaryProviderId ?? "ipify";
        var enabledFallbacks = data?.EnabledFallbackIds?.ToHashSet()
            ?? PublicIpService.AllProviders.Select(p => p.Id).ToHashSet();

        PublicIpProviders.Clear();
        foreach (var (id, name) in PublicIpService.AllProviders)
        {
            var vm = new PublicIpProviderViewModel
            {
                Id = id,
                DisplayName = name,
                IsEnabled = id == primaryId || enabledFallbacks.Contains(id),
                IsPrimary = id == primaryId,
            };
            vm.PropertyChanged += (_, _) => { if (_isLoaded) Save(); };
            PublicIpProviders.Add(vm);
        }
        SelectedPrimaryProvider = PublicIpProviders.FirstOrDefault(p => p.IsPrimary)
            ?? PublicIpProviders.FirstOrDefault();

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
        public bool EnableNotifications { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimized { get; set; }
        public string PrimaryProviderId { get; set; } = "ipify";
        public List<string> EnabledFallbackIds { get; set; } = [];
    }
}

