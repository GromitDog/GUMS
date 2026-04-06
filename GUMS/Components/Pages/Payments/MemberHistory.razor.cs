using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Payments;

public partial class MemberHistory
{
    [Parameter] public string MembershipNumber { get; set; } = string.Empty;

    [Inject] private IPaymentService PaymentService { get; set; } = default!;
    [Inject] private ICreditService CreditService { get; set; } = default!;
    [Inject] private IPersonService PersonService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private int? _personId;
    private MemberPaymentSummary _summary = new();
    private List<Payment> _payments = new();
    private List<Payment> _filteredPayments = new();
    private List<CreditTransaction> _creditHistory = new();
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

    // Convert to credit modal state
    private bool _showConvertToCreditModal;
    private Payment? _paymentToConvert;
    private decimal _convertAmount;
    private string _convertReason = string.Empty;
    private bool _isConverting;

    // Apply credit modal state
    private bool _showApplyCreditModal;
    private Payment? _paymentToApplyCredit;
    private decimal _applyCreditAmount;
    private bool _isApplyingCredit;

    // Refund credit modal state
    private bool _showRefundCreditModal;
    private decimal _refundCreditAmount;
    private PaymentMethod _refundCreditMethod = PaymentMethod.BankTransfer;
    private DateTime _refundCreditDate = DateTime.Today;
    private string _refundCreditReason = string.Empty;
    private bool _isRefundingCredit;

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
            _creditHistory = await CreditService.GetCreditHistoryAsync(MembershipNumber);

            var person = await PersonService.GetByMembershipNumberAsync(MembershipNumber);
            _personId = person?.Id;
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

    // ===== Refund =====

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

    // ===== Convert to Credit =====

    private void ShowConvertToCreditModal(Payment payment)
    {
        _paymentToConvert = payment;
        _convertAmount = payment.AmountPaid;
        _convertReason = string.Empty;
        _showConvertToCreditModal = true;
    }

    private void CancelConvertToCredit()
    {
        _paymentToConvert = null;
        _convertReason = string.Empty;
        _showConvertToCreditModal = false;
    }

    private async Task ConvertToCredit()
    {
        if (_paymentToConvert == null || string.IsNullOrWhiteSpace(_convertReason)) return;

        _isConverting = true;
        _errorMessage = string.Empty;

        try
        {
            var result = await CreditService.ConvertPaymentToCreditAsync(
                _paymentToConvert.Id, _convertAmount, _convertReason);

            if (result.Success)
            {
                _successMessage = $"{_convertAmount:C} converted to credit from '{_paymentToConvert.Reference}'.";
                _showConvertToCreditModal = false;
                _paymentToConvert = null;
                await LoadPaymentHistory();
            }
            else
            {
                _errorMessage = result.ErrorMessage;
                _showConvertToCreditModal = false;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"An error occurred: {ex.Message}";
            _showConvertToCreditModal = false;
        }
        finally
        {
            _isConverting = false;
        }
    }

    // ===== Apply Credit =====

    private void ShowApplyCreditModal(Payment payment)
    {
        _paymentToApplyCredit = payment;
        _applyCreditAmount = Math.Min(_summary.CreditBalance, payment.OutstandingBalance);
        _showApplyCreditModal = true;
    }

    private void CancelApplyCredit()
    {
        _paymentToApplyCredit = null;
        _showApplyCreditModal = false;
    }

    private async Task ApplyCredit()
    {
        if (_paymentToApplyCredit == null) return;

        _isApplyingCredit = true;
        _errorMessage = string.Empty;

        try
        {
            var result = await CreditService.ApplyCreditToPaymentAsync(
                MembershipNumber, _paymentToApplyCredit.Id, _applyCreditAmount);

            if (result.Success)
            {
                _successMessage = $"{_applyCreditAmount:C} credit applied to '{_paymentToApplyCredit.Reference}'.";
                _showApplyCreditModal = false;
                _paymentToApplyCredit = null;
                await LoadPaymentHistory();
            }
            else
            {
                _errorMessage = result.ErrorMessage;
                _showApplyCreditModal = false;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"An error occurred: {ex.Message}";
            _showApplyCreditModal = false;
        }
        finally
        {
            _isApplyingCredit = false;
        }
    }

    // ===== Refund Credit =====

    private void ShowRefundCreditModal()
    {
        _refundCreditAmount = _summary.CreditBalance;
        _refundCreditMethod = PaymentMethod.BankTransfer;
        _refundCreditDate = DateTime.Today;
        _refundCreditReason = string.Empty;
        _showRefundCreditModal = true;
    }

    private void CancelRefundCredit()
    {
        _showRefundCreditModal = false;
    }

    private async Task RefundCredit()
    {
        if (string.IsNullOrWhiteSpace(_refundCreditReason)) return;

        _isRefundingCredit = true;
        _errorMessage = string.Empty;

        try
        {
            var result = await CreditService.RefundCreditAsync(
                MembershipNumber, _refundCreditAmount, _refundCreditMethod, _refundCreditDate, _refundCreditReason);

            if (result.Success)
            {
                _successMessage = $"{_refundCreditAmount:C} credit refunded.";
                _showRefundCreditModal = false;
                await LoadPaymentHistory();
            }
            else
            {
                _errorMessage = result.ErrorMessage;
                _showRefundCreditModal = false;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"An error occurred: {ex.Message}";
            _showRefundCreditModal = false;
        }
        finally
        {
            _isRefundingCredit = false;
        }
    }
}
