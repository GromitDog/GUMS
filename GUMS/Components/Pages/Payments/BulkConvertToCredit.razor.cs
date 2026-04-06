using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Payments;

public partial class BulkConvertToCredit
{
    [Parameter] public int MeetingId { get; set; }

    [Inject] private ICreditService CreditService { get; set; } = default!;
    [Inject] private IPaymentService PaymentService { get; set; } = default!;
    [Inject] private IMeetingService MeetingService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private Meeting? _meeting;
    private List<Payment> _paidPayments = new();
    private List<BulkCreditResult> _results = new();

    private bool _isLoading = true;
    private bool _isProcessing;
    private bool _hasProcessed;
    private bool _autoApplyToSubs = true;
    private string _reason = string.Empty;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;

        try
        {
            _meeting = await MeetingService.GetByIdAsync(MeetingId);
            if (_meeting == null)
            {
                _errorMessage = "Meeting not found.";
                _isLoading = false;
                return;
            }

            var allPayments = await PaymentService.GetByMeetingAsync(MeetingId);
            _paidPayments = allPayments.Where(p => p.Status == PaymentStatus.Paid).ToList();
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

    private async Task ProcessBulkConvert()
    {
        if (string.IsNullOrWhiteSpace(_reason)) return;

        _isProcessing = true;
        _errorMessage = string.Empty;

        try
        {
            var result = await CreditService.BulkConvertMeetingToCreditAsync(MeetingId, _autoApplyToSubs, _reason);

            if (result.Success)
            {
                _results = result.Results;
                _hasProcessed = true;

                var totalConverted = _results.Where(r => r.Success).Sum(r => r.CreditAmount);
                var totalApplied = _results.Where(r => r.Success).Sum(r => r.AppliedToSubs);

                _successMessage = $"Converted {totalConverted:C} to credit across {_results.Count(r => r.Success)} members.";
                if (totalApplied > 0)
                {
                    _successMessage += $" {totalApplied:C} auto-applied to subs.";
                }
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
        finally
        {
            _isProcessing = false;
        }
    }

    private void ClearError() => _errorMessage = string.Empty;
    private void ClearSuccess() => _successMessage = string.Empty;
}
