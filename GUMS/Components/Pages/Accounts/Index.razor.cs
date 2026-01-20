using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class Index
{
    [Inject] private IAccountingService AccountingService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private List<Account> _accounts = new();
    private AccountingDashboardStats _dashboardStats = new();

    private bool _isLoading = true;
    private string _successMessage = string.Empty;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        // Check for success message from navigation state
        var uri = new Uri(NavigationManager.Uri);
        if (uri.Query.Contains("success=deposit"))
        {
            _successMessage = "Bank deposit recorded successfully!";
        }

        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;

        try
        {
            _accounts = await AccountingService.GetAccountsAsync();
            _dashboardStats = await AccountingService.GetDashboardStatsAsync();
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

    private void ClearSuccess()
    {
        _successMessage = string.Empty;
    }

    private void ClearError()
    {
        _errorMessage = string.Empty;
    }
}
