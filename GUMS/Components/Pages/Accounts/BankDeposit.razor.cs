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

    private bool IsFormValid()
    {
        if (_formModel.CashAmount <= 0 && _formModel.ChequeAmount <= 0)
        {
            return false;
        }

        if (_formModel.CashAmount > _cashOnHand)
        {
            return false;
        }

        if (_formModel.ChequeAmount > _chequesPending)
        {
            return false;
        }

        return true;
    }

    private async Task SubmitDeposit()
    {
        if (!IsFormValid()) return;

        _isSubmitting = true;
        _errorMessage = string.Empty;

        try
        {
            var result = await AccountingService.BankDepositAsync(
                _formModel.CashAmount,
                _formModel.ChequeAmount,
                _formModel.DepositDate,
                _formModel.Notes);

            if (result.Success)
            {
                NavigationManager.NavigateTo("/Accounts?success=deposit");
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
