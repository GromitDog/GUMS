using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Payments;

public partial class MemberHistory
{
    [Parameter] public string MembershipNumber { get; set; } = string.Empty;

    [Inject] private IPaymentService PaymentService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private MemberPaymentSummary _summary = new();
    private List<Payment> _payments = new();
    private List<Payment> _filteredPayments = new();
    private PaymentType? _paymentTypeFilter;

    private bool _isLoading = true;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadPaymentHistory();
    }

    private async Task LoadPaymentHistory()
    {
        _isLoading = true;

        try
        {
            _summary = await PaymentService.GetMemberPaymentSummaryAsync(MembershipNumber);
            _payments = await PaymentService.GetByMembershipNumberAsync(MembershipNumber);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading payment history: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void FilterByType(PaymentType? type)
    {
        _paymentTypeFilter = type;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (_paymentTypeFilter == null)
        {
            _filteredPayments = _payments;
        }
        else
        {
            _filteredPayments = _payments
                .Where(p => p.PaymentType == _paymentTypeFilter.Value)
                .ToList();
        }
    }

    private void ClearError()
    {
        _errorMessage = string.Empty;
    }
}
