using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class JournalEntry
{
    [Inject] private IAccountingService AccountingService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private List<Account> _accounts = new();
    private List<JournalLineModel> _lines = new();
    private DateTime _formDate = DateTime.Today;
    private string _formDescription = string.Empty;

    private bool _isLoading = true;
    private bool _isSubmitting;
    private string _errorMessage = string.Empty;

    private decimal TotalDebits => _lines.Sum(l => l.Debit);
    private decimal TotalCredits => _lines.Sum(l => l.Credit);
    private bool IsBalanced => TotalDebits == TotalCredits && TotalDebits > 0;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _accounts = await AccountingService.GetAccountsAsync();
            _lines = new List<JournalLineModel>
            {
                new(),
                new()
            };
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

    private bool IsFormValid()
    {
        if (string.IsNullOrWhiteSpace(_formDescription))
            return false;

        if (!IsBalanced)
            return false;

        foreach (var line in _lines)
        {
            if (line.AccountId == 0)
                return false;

            if (line.Debit <= 0 && line.Credit <= 0)
                return false;
        }

        return true;
    }

    private void AddLine()
    {
        _lines.Add(new JournalLineModel());
    }

    private void RemoveLine(JournalLineModel line)
    {
        if (_lines.Count > 2)
        {
            _lines.Remove(line);
        }
    }

    private async Task SubmitJournalEntry()
    {
        if (!IsFormValid() || _isSubmitting)
            return;

        _isSubmitting = true;
        _errorMessage = string.Empty;

        try
        {
            var transaction = new Transaction
            {
                Date = _formDate,
                Description = _formDescription,
                Lines = _lines.Select(l => new TransactionLine
                {
                    AccountId = l.AccountId,
                    Debit = l.Debit,
                    Credit = l.Credit
                }).ToList()
            };

            var result = await AccountingService.CreateTransactionAsync(transaction);

            if (result.Success)
            {
                NavigationManager.NavigateTo("/Accounts/Transactions?success=journal");
            }
            else
            {
                _errorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error creating journal entry: {ex.Message}";
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private class JournalLineModel
    {
        public int AccountId { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }
}
