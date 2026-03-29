using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;

namespace GUMS.Components.Pages.Configuration;

[Authorize]
public partial class UserManagement
{
    [Inject] public required UserManager<IdentityUser> UserManager { get; set; }
    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; set; }

    private List<IdentityUser> _users = new();
    private string _currentUserId = string.Empty;
    private bool _isLoading = true;
    private bool _isSaving;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private void ClearError() => _errorMessage = string.Empty;
    private void ClearSuccess() => _successMessage = string.Empty;

    // Add user form
    private bool _showAddForm;
    private string _newEmail = string.Empty;
    private string _newPassword = string.Empty;
    private string _newPasswordConfirm = string.Empty;

    // Delete confirmation
    private bool _showDeleteConfirm;
    private IdentityUser? _userToDelete;
    private bool _isDeleting;

    // Reset password
    private bool _showResetPassword;
    private IdentityUser? _userToReset;
    private string _resetPassword = string.Empty;
    private string _resetPasswordConfirm = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var currentUser = await UserManager.GetUserAsync(authState.User);
        _currentUserId = currentUser?.Id ?? string.Empty;
        await LoadUsers();
    }

    private async Task LoadUsers()
    {
        _isLoading = true;
        _users = await Task.Run(() => UserManager.Users.OrderBy(u => u.Email).ToList());
        _isLoading = false;
    }

    private void ShowAddForm()
    {
        _newEmail = string.Empty;
        _newPassword = string.Empty;
        _newPasswordConfirm = string.Empty;
        _showAddForm = true;
    }

    private void CancelAddForm()
    {
        _showAddForm = false;
    }

    private async Task AddUser()
    {
        _errorMessage = string.Empty;
        _successMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(_newEmail))
        {
            _errorMessage = "Email is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_newPassword))
        {
            _errorMessage = "Password is required.";
            return;
        }

        if (_newPassword != _newPasswordConfirm)
        {
            _errorMessage = "Passwords do not match.";
            return;
        }

        _isSaving = true;
        var user = new IdentityUser { UserName = _newEmail.Trim(), Email = _newEmail.Trim(), EmailConfirmed = true };
        var result = await UserManager.CreateAsync(user, _newPassword);

        if (result.Succeeded)
        {
            _successMessage = $"User {_newEmail.Trim()} created successfully.";
            _showAddForm = false;
            await LoadUsers();
        }
        else
        {
            _errorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
        }

        _isSaving = false;
    }

    private void ConfirmDelete(IdentityUser user)
    {
        _userToDelete = user;
        _showDeleteConfirm = true;
    }

    private void CancelDelete()
    {
        _userToDelete = null;
        _showDeleteConfirm = false;
    }

    private async Task DeleteUser()
    {
        if (_userToDelete == null) return;

        if (_userToDelete.Id == _currentUserId)
        {
            _errorMessage = "You cannot delete your own account.";
            _showDeleteConfirm = false;
            _userToDelete = null;
            return;
        }

        _isDeleting = true;
        var result = await UserManager.DeleteAsync(_userToDelete);

        if (result.Succeeded)
        {
            _successMessage = $"User {_userToDelete.Email} deleted.";
            await LoadUsers();
        }
        else
        {
            _errorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
        }

        _isDeleting = false;
        _showDeleteConfirm = false;
        _userToDelete = null;
    }

    private void ShowResetPassword(IdentityUser user)
    {
        _userToReset = user;
        _resetPassword = string.Empty;
        _resetPasswordConfirm = string.Empty;
        _showResetPassword = true;
    }

    private void CancelResetPassword()
    {
        _userToReset = null;
        _showResetPassword = false;
    }

    private async Task ResetPassword()
    {
        _errorMessage = string.Empty;
        _successMessage = string.Empty;

        if (_userToReset == null) return;

        if (string.IsNullOrWhiteSpace(_resetPassword))
        {
            _errorMessage = "Password is required.";
            return;
        }

        if (_resetPassword != _resetPasswordConfirm)
        {
            _errorMessage = "Passwords do not match.";
            return;
        }

        _isSaving = true;
        var token = await UserManager.GeneratePasswordResetTokenAsync(_userToReset);
        var result = await UserManager.ResetPasswordAsync(_userToReset, token, _resetPassword);

        if (result.Succeeded)
        {
            _successMessage = $"Password reset for {_userToReset.Email}.";
            _showResetPassword = false;
            _userToReset = null;
        }
        else
        {
            _errorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
        }

        _isSaving = false;
    }
}
