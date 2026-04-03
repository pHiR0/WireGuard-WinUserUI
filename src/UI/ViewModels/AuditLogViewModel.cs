using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WireGuard.Shared.IPC;
using WireGuard.Shared.Models;
using WireGuard.UI.Services;

namespace WireGuard.UI.ViewModels;

public partial class AuditLogViewModel : ViewModelBase
{
    private readonly IPipeClient _pipeClient;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    // Filters
    [ObservableProperty]
    private string _filterUsername = string.Empty;

    [ObservableProperty]
    private string _filterAction = string.Empty;

    // Pagination
    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _pageSize = 50;

    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
    public bool CanGoBack => CurrentPage > 1;
    public bool CanGoForward => CurrentPage < TotalPages;

    public ObservableCollection<AuditEntryDto> Entries { get; } = [];

    public AuditLogViewModel(IPipeClient pipeClient)
    {
        _pipeClient = pipeClient;
    }

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    partial void OnTotalCountChanged(int value)
    {
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(CanGoForward));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            var query = new AuditQuery
            {
                Username = string.IsNullOrWhiteSpace(FilterUsername) ? null : FilterUsername.Trim(),
                Action = string.IsNullOrWhiteSpace(FilterAction) ? null : FilterAction.Trim(),
                Page = CurrentPage,
                PageSize = PageSize,
            };

            var page = await _pipeClient.GetAuditLogAsync(query);
            TotalCount = page.TotalCount;

            Entries.Clear();
            foreach (var e in page.Entries)
                Entries.Add(e);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (!CanGoForward) return;
        CurrentPage++;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (!CanGoBack) return;
        CurrentPage--;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        CurrentPage = 1;
        await LoadAsync();
    }
}
