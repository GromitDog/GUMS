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
    private string _successMessage = string.Empty;

    // Refund modal state
    private bool _showRefundConfirm;
    private Payment? _paymentToRefund;
    private decimal _refundAmount;
    private PaymentMethod _refundMethod;
    private DateTime _refundDate = DateTime.Today;
    private string _refundReason = string.Empty;
    private bool _isRefunding;

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

    private void ClearSuccess()
    {
        _successMessage = string.Empty;
    }

    private void ShowRefundConfirm(Payment payment)
    {
        _paymentToRefund = payment;
        _refundAmount = payment.AmountPaid;
        _refundMethod = PaymentMethod.BankTransfer;
        _refundDate = DateTime.Today;
        _refundReason = string.Empty;
        _showRefundConfirm = true;
    }

    private void CancelRefundConfirm()
    {
        _paymentToRefund = null;
        _refundReason = string.Empty;
        _showRefundConfirm = false;
    }

    private async Task RefundPayment()
    {
        if (_paymentToRefund == null || string.IsNullOrWhiteSpace(_refundReason)) return;

        _isRefunding = true;
        _errorMessage = string.Empty;

        try
        {
            var result = await PaymentService.RefundPaymentAsync(
                _paymentToRefund.Id, _refundAmount, _refundMethod, _refundDate, _refundReason);

            if (result.Success)
            {
                _successMessage = $"Payment '{_paymentToRefund.Reference}' refunded successfully!";
                _showRefundConfirm = false;
                _paymentToRefund = null;
                _refundReason = string.Empty;
                await LoadPaymentHistory();
            }
            else
            {
                _errorMessage = result.ErrorMessage;
                _showRefundConfirm = false;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"An error occurred: {ex.Message}";
            _showRefundConfirm = false;
        }
        finally
        {
            _isRefunding = false;
        }
    }
}
