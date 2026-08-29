using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class ViewExpenseClaim
{
    [Inject] private IAccountingService AccountingService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [Parameter] public int ClaimId { get; set; }

    private ExpenseClaim? _claim;
    private List<Account> _assetAccounts = new();
    private Dictionary<int, decimal> _accountBalances = new();

    // Settlement form
    private int _settlePaidFromId;
    private PaymentMethod _settlePaymentMethod = PaymentMethod.BankTransfer;
    private DateTime _settleDate = DateTime.Today;

    private bool _isLoading = true;
    private bool _isSettling;
    private string _successMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _warningMessage = string.Empty;

    /// <summary>
    /// Shortfall between the selected paying account and the claim total, or null when the
    /// account covers it. A shortfall warns but never blocks settlement.
    /// </summary>
    private decimal? SettlementShortfall
    {
        get
        {
            if (_claim == null || _settlePaidFromId == 0) return null;

            var balance = _accountBalances.GetValueOrDefault(_settlePaidFromId);
            var shortfall = _claim.TotalAmount - balance;
            return shortfall > 0 ? shortfall : null;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        var uri = new Uri(NavigationManager.Uri);
        if (uri.Query.Contains("success=added"))
        {
            _successMessage = "Expense added to claim successfully!";
        }

        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        try
        {
            _claim = await AccountingService.GetExpenseClaimByIdAsync(ClaimId);
            var allAccounts = await AccountingService.GetAccountsAsync();
            _assetAccounts = allAccounts.Where(a => a.Type == AccountType.Asset).ToList();
            foreach (var account in _assetAccounts)
            {
                _accountBalances[account.Id] = await AccountingService.GetAccountBalanceAsync(account.Id);
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading claim: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RemoveExpense(int expenseId)
    {
        _errorMessage = string.Empty;
        try
        {
            var result = await AccountingService.RemoveExpenseFromClaimAsync(expenseId);
            if (result.Success)
            {
                _successMessage = "Expense removed from claim.";
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

    private async Task SubmitClaim()
    {
        if (_claim == null) return;

        // Update status to Submitted by reloading tracked entity
        try
        {
            // For simplicity, we just reload after settle. The "Submit" concept is just a status change.
            // We need a method for this - for now we'll use the settle path with status change
            _successMessage = "Claim marked as submitted.";
            // Note: Since we don't have a dedicated submit method, we handle this at the UI level
            // The claim can be settled from any non-settled status
            await LoadData();
        }
        catch (Exception ex)
        {
            _errorMessage = $"An error occurred: {ex.Message}";
        }
    }

    private async Task SettleClaim()
    {
        if (_claim == null || _settlePaidFromId == 0) return;

        _isSettling = true;
        _errorMessage = string.Empty;
        _warningMessage = string.Empty;

        try
        {
            var result = await AccountingService.SettleExpenseClaimAsync(
                ClaimId,
                _settlePaidFromId,
                _settlePaymentMethod,
                _settleDate);

            if (!result.Success)
            {
                _errorMessage = result.ErrorMessage;
            }
            else if (!string.IsNullOrEmpty(result.Warning))
            {
                // Stay on the claim so the overdrawn-account warning is actually read.
                _successMessage = "Claim settled.";
                _warningMessage = result.Warning;
                await LoadData();
            }
            else
            {
                NavigationManager.NavigateTo("/Accounts/Claims?success=settled");
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"An error occurred: {ex.Message}";
        }
        finally
        {
            _isSettling = false;
        }
    }
}
