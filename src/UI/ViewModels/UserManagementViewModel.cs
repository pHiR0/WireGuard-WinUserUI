using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WireGuard.Shared.Models;
using WireGuard.UI.Services;

namespace WireGuard.UI.ViewModels;

public partial class UserManagementViewModel : ViewModelBase
{
    private readonly IPipeClient _pipeClient;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    // Add user form
    [ObservableProperty]
    private string _newUsername = string.Empty;

    [ObservableProperty]
    private UserRole _newUserRole = UserRole.Viewer;

    public ObservableCollection<UserViewModel> Users { get; } = [];

    public UserRole[] AvailableRoles { get; } = [UserRole.Viewer, UserRole.Operator, UserRole.AdvancedOperator, UserRole.Admin];

    public UserManagementViewModel(IPipeClient pipeClient)
    {
        _pipeClient = pipeClient;
    }

    [RelayCommand]
    public async Task LoadUsersAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            var users = await _pipeClient.ListUsersAsync();
            Users.Clear();
            foreach (var u in users)
            {
                Users.Add(new UserViewModel
                {
                    Username = u.Username,
                    Role = u.Role,
                });
            }
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
    private async Task AddUserAsync()
    {
        if (string.IsNullOrWhiteSpace(NewUsername)) return;

        try
        {
            ErrorMessage = string.Empty;
            await _pipeClient.SetUserRoleAsync(NewUsername.Trim(), NewUserRole);
            NewUsername = string.Empty;
            NewUserRole = UserRole.Viewer;
            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ChangeRoleAsync(UserViewModel? user)
    {
        if (user is null) return;

        try
        {
            ErrorMessage = string.Empty;
            await _pipeClient.SetUserRoleAsync(user.Username, user.Role);
            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task RemoveUserAsync(UserViewModel? user)
    {
        if (user is null) return;

        try
        {
            ErrorMessage = string.Empty;
            await _pipeClient.RemoveUserAsync(user.Username);
            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}

public partial class UserViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private UserRole _role;
}
