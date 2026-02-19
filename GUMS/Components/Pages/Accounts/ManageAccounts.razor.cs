using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class ManageAccounts
{
    [Inject] private IAccountingService AccountingService { get; set; } = default!;

    private readonly AccountType[] _accountTypes = { AccountType.Asset, AccountType.Liability, AccountType.Equity, AccountType.Income, AccountType.Expense };
    private AccountType _selectedType = AccountType.Asset;
    private List<Account> _accounts = new();
    private string _newAccountName = string.Empty;
    private int? _editingAccountId;
    private string _editingName = string.Empty;

    private bool _isLoading = true;
    private bool _isSubmitting;
    private string _successMessage = string.Empty;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task SelectType(AccountType type)
    {
        _selectedType = type;
        _editingAccountId = null;
        _editingName = string.Empty;
        _newAccountName = string.Empty;
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        try
        {
            _accounts = await AccountingService.GetAccountsByTypeAsync(_selectedType);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading accounts: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task CreateAccount()
    {
        if (string.IsNullOrWhiteSpace(_newAccountName)) return;

        _isSubmitting = true;
        _errorMessage = string.Empty;

        try
        {
            var result = await AccountingService.CreateAccountAsync(_newAccountName.Trim(), _selectedType);
            if (result.Success)
            {
                _successMessage = $"Account '{_newAccountName}' created successfully.";
                _newAccountName = string.Empty;
                await LoadData();
            }
            else
            {
                _errorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"An error occurred: {ex.Message}";
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private void StartEdit(Account account)
    {
        _editingAccountId = account.Id;
        _editingName = account.Name;
    }

    private void CancelEdit()
    {
        _editingAccountId = null;
        _editingName = string.Empty;
    }

    private async Task SaveEdit()
    {
        if (_editingAccountId == null || string.IsNullOrWhiteSpace(_editingName)) return;

        _errorMessage = string.Empty;

        try
        {
            var result = await AccountingService.UpdateAccountAsync(_editingAccountId.Value, _editingName.Trim());
            if (result.Success)
            {
                _successMessage = "Account updated successfully.";
                _editingAccountId = null;
                _editingName = string.Empty;
                await LoadData();
            }
            else
            {
                _errorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"An error occurred: {ex.Message}";
        }
    }

    private async Task DeleteAccount(int accountId)
    {
        _errorMessage = string.Empty;

        try
        {
            var result = await AccountingService.DeleteAccountAsync(accountId);
            if (result.Success)
            {
                _successMessage = "Account deleted successfully.";
                await LoadData();
            }
            else
            {
                _errorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"An error occurred: {ex.Message}";
        }
    }

    private static string GetTypeIcon(AccountType type) => type switch
    {
        AccountType.Asset => "bi-safe",
        AccountType.Liability => "bi-credit-card",
        AccountType.Equity => "bi-piggy-bank",
        AccountType.Income => "bi-arrow-down-circle",
        AccountType.Expense => "bi-arrow-up-circle",
        _ => "bi-folder"
    };
}
