using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class RecordIncome
{
    [Inject] private IAccountingService AccountingService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private List<Account> _incomeAccounts = new();
    private List<Account> _expenseAccounts = new();
    private List<Account> _assetAccounts = new();
    private Dictionary<int, decimal> _accountBalances = new();

    // Form fields
    private DateTime _formDate = DateTime.Today;
    private decimal _formAmount;
    private int _formCreditAccountId;
    private int _formReceivedIntoId;
    private string _formDescription = string.Empty;
    private string? _formReference;
    private string? _formNotes;

    private bool _isLoading = true;
    private bool _isSubmitting;
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
            _incomeAccounts = await AccountingService.GetAccountsByTypeAsync(AccountType.Income);
            _expenseAccounts = await AccountingService.GetAccountsByTypeAsync(AccountType.Expense);
            var allAccounts = await AccountingService.GetAccountsAsync();
            _assetAccounts = allAccounts.Where(a => a.Type == AccountType.Asset).ToList();
            foreach (var account in _assetAccounts)
            {
                _accountBalances[account.Id] = await AccountingService.GetAccountBalanceAsync(account.Id);
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading data: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private bool IsFormValid()
    {
        if (_formAmount <= 0) return false;
        if (_formCreditAccountId == 0) return false;
        if (_formReceivedIntoId == 0) return false;
        if (string.IsNullOrWhiteSpace(_formDescription)) return false;
        return true;
    }

    private async Task SubmitIncome()
    {
        if (!IsFormValid()) return;

        _isSubmitting = true;
        _errorMessage = string.Empty;

        try
        {
            var result = await AccountingService.RecordDirectIncomeAsync(
                _formAmount,
                _formCreditAccountId,
                _formReceivedIntoId,
                _formDescription.Trim(),
                _formDate,
                string.IsNullOrWhiteSpace(_formReference) ? null : _formReference.Trim(),
                string.IsNullOrWhiteSpace(_formNotes) ? null : _formNotes.Trim());

            if (result.Success)
            {
                NavigationManager.NavigateTo("/Accounts?success=income");
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
}
