using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class ExpenseList
{
    [Inject] private IAccountingService AccountingService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private List<Expense> _expenses = new();
    private List<Account> _expenseAccounts = new();

    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private int _filterCategoryId;

    private bool _isLoading = true;
    private string _successMessage = string.Empty;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        var uri = new Uri(NavigationManager.Uri);
        if (uri.Query.Contains("success=recorded"))
        {
            _successMessage = "Expense recorded successfully!";
        }

        _expenseAccounts = await AccountingService.GetExpenseAccountsAsync();
        await ApplyFilters();
    }

    private async Task ApplyFilters()
    {
        _isLoading = true;
        try
        {
            _expenses = await AccountingService.GetExpensesAsync(
                _dateFrom,
                _dateTo,
                _filterCategoryId > 0 ? _filterCategoryId : null);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading expenses: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task DeleteExpense(int expenseId)
    {
        _errorMessage = string.Empty;
        try
        {
            var result = await AccountingService.DeleteDirectExpenseAsync(expenseId);
            if (result.Success)
            {
                _successMessage = "Expense deleted and transaction reversed.";
                await ApplyFilters();
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
