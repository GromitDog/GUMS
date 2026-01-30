using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class BudgetComparison
{
    [Inject] private IBudgetService BudgetService { get; set; } = default!;

    [Parameter] public int MeetingId { get; set; }

    private BudgetVsActual? _comparison;
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;
        try
        {
            _comparison = await BudgetService.GetBudgetVsActualAsync(MeetingId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading budget comparison: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }
}
