using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class Transactions
{
    [Inject] private IAccountingService AccountingService { get; set; } = default!;

    private List<Transaction> _transactions = new();
    private DateTime? _dateFrom;
    private DateTime? _dateTo;

    private bool _isLoading = true;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
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
}
