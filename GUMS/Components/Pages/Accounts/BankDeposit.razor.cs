using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class BankDeposit
{
    [Inject] private IAccountingService AccountingService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private decimal _cashOnHand;
    private decimal _chequesPending;
    private decimal _bankBalance;
    private BankDepositFormModel _formModel = new();

    private bool _isLoading = true;
    private bool _isSubmitting;
    private string _errorMessage = string.Empty;
    private string _warningMessage = string.Empty;
    private string _successMessage = string.Empty;

    private decimal CashOverBanked => Math.Max(0, _formModel.CashAmount - _cashOnHand);
    private decimal ChequesOverBanked => Math.Max(0, _formModel.ChequeAmount - _chequesPending);

    protected override async Task OnInitializedAsync()
    {
        await LoadBalances();
    }

    private async Task LoadBalances()
    {
        _isLoading = true;

        try
        {
            _cashOnHand = await AccountingService.GetCashOnHandAsync();
            _chequesPending = await AccountingService.GetChequesPendingAsync();
            _bankBalance = await AccountingService.GetBankBalanceAsync();

            // Initialize form with defaults
            _formModel = new BankDepositFormModel
            {
                DepositDate = DateTime.Today
            };
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading balances: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Only the total matters: banking more than the recorded balance warns rather than blocks,
    /// since cheques or cash can reach the bank without having been logged first.
    /// </summary>
    private bool IsFormValid()
    {
        return _formModel.CashAmount > 0 || _formModel.ChequeAmount > 0;
    }

    private async Task SubmitDeposit()
    {
        if (!IsFormValid()) return;

        _isSubmitting = true;
        _errorMessage = string.Empty;
        _warningMessage = string.Empty;

        try
        {
            var result = await AccountingService.BankDepositAsync(
                _formModel.CashAmount,
                _formModel.ChequeAmount,
                _formModel.DepositDate,
                _formModel.Notes);

            if (!result.Success)
            {
                _errorMessage = result.ErrorMessage;
            }
            else if (!string.IsNullOrEmpty(result.Warning))
            {
                // Stay on the page so the discrepancy warning is actually read.
                _successMessage = "Deposit recorded.";
                _warningMessage = result.Warning;
                await LoadBalances();
            }
            else
            {
                NavigationManager.NavigateTo("/Accounts?success=deposit");
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

    private void ClearError()
    {
        _errorMessage = string.Empty;
    }

    public class BankDepositFormModel
    {
        public decimal CashAmount { get; set; }
        public decimal ChequeAmount { get; set; }
        public DateTime DepositDate { get; set; } = DateTime.Today;
        public string? Notes { get; set; }
    }
}
