using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class Transactions
{
    [Inject] private IAccountingService AccountingService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private List<Transaction> _transactions = new();
    private DateTime? _dateFrom;
    private DateTime? _dateTo;

    private bool _isLoading = true;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private int? _confirmVoidId;

    protected override async Task OnInitializedAsync()
    {
        var uri = new Uri(NavigationManager.Uri);
        if (uri.Query.Contains("success=journal"))
        {
            _successMessage = "Journal entry posted successfully!";
        }
        else if (uri.Query.Contains("success=voided"))
        {
            _successMessage = "Transaction voided successfully.";
        }

        // Default to last 30 days
        _dateTo = DateTime.Today;
        _dateFrom = DateTime.Today.AddDays(-30);

        await LoadTransactions();
    }

    private async Task LoadTransactions()
    {
        _isLoading = true;

        try
        {
            _transactions = await AccountingService.GetTransactionsAsync(_dateFrom, _dateTo);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading transactions: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ApplyFilter()
    {
        await LoadTransactions();
    }

    private async Task ClearFilter()
    {
        _dateFrom = null;
        _dateTo = null;
        await LoadTransactions();
    }

    private void ClearError()
    {
        _errorMessage = string.Empty;
    }

    private void ClearSuccess()
    {
        _successMessage = string.Empty;
    }

    private async Task VoidTransaction(int transactionId)
    {
        if (_confirmVoidId != transactionId)
        {
            _confirmVoidId = transactionId;
            return;
        }

        _confirmVoidId = null;

        var result = await AccountingService.VoidTransactionAsync(transactionId);
        if (result.Success)
        {
            _successMessage = "Transaction voided successfully. Account balances have been reversed.";
            _errorMessage = string.Empty;
            await LoadTransactions();
        }
        else
        {
            _errorMessage = result.ErrorMessage;
        }
    }

    private void CancelVoid()
    {
        _confirmVoidId = null;
    }
}
