using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class ExpenseClaims
{
    [Inject] private IAccountingService AccountingService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private List<ExpenseClaim> _claims = new();
    private ExpenseClaimStatus? _filterStatus;

    private bool _isLoading = true;
    private string _successMessage = string.Empty;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        var uri = new Uri(NavigationManager.Uri);
        if (uri.Query.Contains("success=settled"))
        {
            _successMessage = "Expense claim settled successfully!";
        }

        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        try
        {
            _claims = await AccountingService.GetExpenseClaimsAsync(_filterStatus);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading claims: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task FilterByStatus(ExpenseClaimStatus? status)
    {
        _filterStatus = status;
        await LoadData();
    }

    private async Task DeleteClaim(int claimId)
    {
        _errorMessage = string.Empty;
        try
        {
            var result = await AccountingService.DeleteExpenseClaimAsync(claimId);
            if (result.Success)
            {
                _successMessage = "Claim deleted successfully.";
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
