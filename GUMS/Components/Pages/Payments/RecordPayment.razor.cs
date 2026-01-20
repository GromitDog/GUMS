using System.ComponentModel.DataAnnotations;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Payments;

public partial class RecordPayment
{
    [Parameter] public int PaymentId { get; set; }

    [Inject] private IPaymentService PaymentService { get; set; } = default!;
    [Inject] private IPersonService PersonService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private Payment? _payment;
    private string _memberName = string.Empty;
    private RecordPaymentFormModel _formModel = new();

    private bool _isLoading = true;
    private bool _isSubmitting;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadPayment();
    }

    private async Task LoadPayment()
    {
        _isLoading = true;

        try
        {
            _payment = await PaymentService.GetByIdAsync(PaymentId);

            if (_payment != null)
            {
                // Get member name
                var person = await PersonService.GetByMembershipNumberAsync(_payment.MembershipNumber);
                _memberName = person?.FullName ?? _payment.MembershipNumber;

                // Initialize form with defaults
                _formModel = new RecordPaymentFormModel
                {
                    Amount = _payment.OutstandingBalance,
                    PaymentDate = DateTime.Today
                };
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading payment: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void SetAmount(decimal amount)
    {
        _formModel.Amount = amount;
    }

    private bool IsFormValid()
    {
        if (_payment == null) return false;
        if (_formModel.PaymentMethod == null) return false;
        if (_formModel.Amount <= 0) return false;
        if (_formModel.Amount > _payment.OutstandingBalance) return false;
        return true;
    }

    private async Task SubmitPayment()
    {
        if (_payment == null || !IsFormValid()) return;

        _isSubmitting = true;
        _errorMessage = string.Empty;

        try
        {
            var result = await PaymentService.RecordPaymentAsync(
                _payment.Id,
                _formModel.Amount,
                _formModel.PaymentMethod!.Value,
                _formModel.PaymentDate,
                _formModel.Notes);

            if (result.Success)
            {
                NavigationManager.NavigateTo("/Payments?success=recorded");
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
            _isSubmitting = false;
        }
    }

    private void ClearError()
    {
        _errorMessage = string.Empty;
    }

    public class RecordPaymentFormModel
    {
        [Required(ErrorMessage = "Payment method is required")]
        public PaymentMethod? PaymentMethod { get; set; }

        [Required(ErrorMessage = "Amount is required")]
        [Range(0.01, 10000, ErrorMessage = "Amount must be between £0.01 and £10,000")]
        public decimal Amount { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Today;

        public string? Notes { get; set; }
    }
}
