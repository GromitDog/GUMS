using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class EventAccounts
{
    [Inject] private IAccountingService AccountingService { get; set; } = default!;

    [Parameter] public int MeetingId { get; set; }

    private EventFinancialSummary? _summary;
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;
        try
        {
            _summary = await AccountingService.GetEventFinancialSummaryAsync(MeetingId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading event summary: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }
}
