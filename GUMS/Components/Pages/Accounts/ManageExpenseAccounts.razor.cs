using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class ManageExpenseAccounts
{
    [Inject] private IAccountingService AccountingService { get; set; } = default!;

    private List<Account> _expenseAccounts = new();
    private string _newCategoryName = string.Empty;
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

    private async Task LoadData()
    {
        _isLoading = true;
        try
        {
            _expenseAccounts = await AccountingService.GetExpenseAccountsAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading categories: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task CreateCategory()
    {
        if (string.IsNullOrWhiteSpace(_newCategoryName)) return;

        _isSubmitting = true;
        _errorMessage = string.Empty;

        try
        {
            var result = await AccountingService.CreateExpenseAccountAsync(_newCategoryName.Trim());
            if (result.Success)
            {
                _successMessage = $"Category '{_newCategoryName}' created successfully.";
                _newCategoryName = string.Empty;
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
            var result = await AccountingService.UpdateExpenseAccountAsync(_editingAccountId.Value, _editingName.Trim());
            if (result.Success)
            {
                _successMessage = "Category updated successfully.";
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

    private async Task DeleteCategory(int accountId)
    {
        _errorMessage = string.Empty;

        try
        {
            var result = await AccountingService.DeleteExpenseAccountAsync(accountId);
            if (result.Success)
            {
                _successMessage = "Category deleted successfully.";
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
}
