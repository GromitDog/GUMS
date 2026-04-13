using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class RecordExpense
{
    [Inject] private IAccountingService AccountingService { get; set; } = default!;
    [Inject] private IMeetingService MeetingService { get; set; } = default!;
    [Inject] private IPersonService PersonService { get; set; } = default!;
    [Inject] private ICostCentreService CostCentreService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "claimId")]
    public int? PreselectedClaimId { get; set; }

    private List<Account> _expenseAccounts = new();
    private List<Account> _assetAccounts = new();
    private Dictionary<int, decimal> _accountBalances = new();
    private List<Meeting> _meetings = new();
    private List<ExpenseClaim> _draftClaims = new();
    private List<Person> _leaders = new();
    private List<CostCentre> _costCentres = new();

    private string _expenseType = "direct";
    private int _selectedClaimId;
    private int _newClaimLeaderId;

    // Form fields
    private DateTime _formDate = DateTime.Today;
    private decimal _formAmount;
    private int _formCategoryId;
    private int _formPaidFromId;
    private string _formDescription = string.Empty;
    private string? _formReference;
    private string? _formNotes;
    private int _formMeetingId;
    private int _formCostCentreId;

    private bool _isLoading = true;
    private bool _isSubmitting;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();

        if (PreselectedClaimId > 0)
        {
            _expenseType = "claim";
            _selectedClaimId = PreselectedClaimId.Value;
        }
    }

    private async Task LoadData()
    {
        _isLoading = true;
        try
        {
            _expenseAccounts = await AccountingService.GetExpenseAccountsAsync();
            var allAccounts = await AccountingService.GetAccountsAsync();
            _assetAccounts = allAccounts.Where(a => a.Type == AccountType.Asset).ToList();
            foreach (var account in _assetAccounts)
            {
                _accountBalances[account.Id] = await AccountingService.GetAccountBalanceAsync(account.Id);
            }
            _meetings = await MeetingService.GetAllAsync();
            _draftClaims = await AccountingService.GetExpenseClaimsAsync(ExpenseClaimStatus.Draft);
            _leaders = await PersonService.GetByTypeAsync(PersonType.Leader);
            _costCentres = await CostCentreService.GetAllAsync();
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

    private void OnMeetingChanged(ChangeEventArgs e)
    {
        _formMeetingId = int.TryParse(e.Value?.ToString(), out var id) ? id : 0;

        // Auto-fill cost centre from meeting's default
        if (_formMeetingId > 0)
        {
            var meeting = _meetings.FirstOrDefault(m => m.Id == _formMeetingId);
            if (meeting?.CostCentreId > 0)
            {
                _formCostCentreId = meeting.CostCentreId.Value;
            }
        }
    }

    private bool IsFormValid()
    {
        if (_formAmount <= 0) return false;
        if (_formCategoryId == 0) return false;
        if (string.IsNullOrWhiteSpace(_formDescription)) return false;

        if (_expenseType == "direct" && _formPaidFromId == 0) return false;
        if (_expenseType == "claim" && _selectedClaimId == 0 && _newClaimLeaderId == 0) return false;

        return true;
    }

    private async Task SubmitExpense()
    {
        if (!IsFormValid()) return;

        _isSubmitting = true;
        _errorMessage = string.Empty;

        try
        {
            var expense = new Expense
            {
                Date = _formDate,
                Amount = _formAmount,
                ExpenseAccountId = _formCategoryId,
                Description = _formDescription.Trim(),
                Reference = string.IsNullOrWhiteSpace(_formReference) ? null : _formReference.Trim(),
                Notes = string.IsNullOrWhiteSpace(_formNotes) ? null : _formNotes.Trim(),
                MeetingId = _formMeetingId > 0 ? _formMeetingId : null,
                CostCentreId = _formCostCentreId > 0 ? _formCostCentreId : null
            };

            if (_expenseType == "direct")
            {
                expense.PaidFromAccountId = _formPaidFromId;
                var result = await AccountingService.RecordDirectExpenseAsync(expense);
                if (result.Success)
                {
                    NavigationManager.NavigateTo("/Accounts/Expenses?success=recorded");
                }
                else
                {
                    _errorMessage = result.ErrorMessage;
                }
            }
            else
            {
                // Reimbursement claim
                int claimId = _selectedClaimId;

                if (claimId == 0)
                {
                    var leader = _leaders.First(l => l.Id == _newClaimLeaderId);
                    // Create new claim
                    var claimResult = await AccountingService.CreateExpenseClaimAsync(new ExpenseClaim
                    {
                        ClaimedBy = leader.FullName,
                        SubmittedDate = DateTime.Today
                    });

                    if (!claimResult.Success)
                    {
                        _errorMessage = claimResult.ErrorMessage;
                        return;
                    }
                    claimId = claimResult.Claim!.Id;
                }

                var addResult = await AccountingService.AddExpenseToClaimAsync(claimId, expense);
                if (addResult.Success)
                {
                    NavigationManager.NavigateTo($"/Accounts/Claims/{claimId}?success=added");
                }
                else
                {
                    _errorMessage = addResult.ErrorMessage;
                }
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
