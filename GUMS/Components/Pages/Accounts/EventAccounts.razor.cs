using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class EventAccounts
{
    [Inject] private IAccountingService AccountingService { get; set; } = default!;
    [Inject] private IPaymentService PaymentService { get; set; } = default!;

    [Parameter] public int MeetingId { get; set; }

    private EventFinancialSummary? _summary;
    private List<Payment> _unlinkedPayments = new();
    private HashSet<int> _selectedPaymentIds = new();
    private bool _isLoading = true;
    private bool _isLinking;
    private string _linkError = string.Empty;
    private string _linkSuccess = string.Empty;
    private bool _showLinkPanel;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        try
        {
            _summary = await AccountingService.GetEventFinancialSummaryAsync(MeetingId);
            _unlinkedPayments = await PaymentService.GetUnlinkedPaidOtherPaymentsAsync();
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

    private void ToggleLinkPanel() => _showLinkPanel = !_showLinkPanel;

    private void TogglePayment(int paymentId)
    {
        if (!_selectedPaymentIds.Remove(paymentId))
            _selectedPaymentIds.Add(paymentId);
    }

    private async Task LinkSelectedPayments()
    {
        if (!_selectedPaymentIds.Any()) return;

        _isLinking = true;
        _linkError = string.Empty;
        _linkSuccess = string.Empty;

        try
        {
            int linked = 0;
            foreach (var id in _selectedPaymentIds)
            {
                var result = await PaymentService.SetMeetingLinkAsync(id, MeetingId);
                if (result.Success) linked++;
            }

            _selectedPaymentIds.Clear();
            _showLinkPanel = false;
            _linkSuccess = $"{linked} payment{(linked != 1 ? "s" : "")} linked to this event.";
            await LoadData();
        }
        catch (Exception ex)
        {
            _linkError = $"Error linking payments: {ex.Message}";
        }
        finally
        {
            _isLinking = false;
        }
    }

    private static string FormatPaymentMethod(PaymentMethod method) => method switch
    {
        Data.Enums.PaymentMethod.Cash => "Cash",
        Data.Enums.PaymentMethod.Cheque => "Cheque",
        Data.Enums.PaymentMethod.BankTransfer => "Bank Transfer",
        _ => method.ToString()
    };
}
