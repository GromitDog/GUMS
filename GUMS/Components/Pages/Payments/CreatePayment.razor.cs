using System.ComponentModel.DataAnnotations;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Payments;

public partial class CreatePayment
{
    [Inject] private IPaymentService PaymentService { get; set; } = default!;
    [Inject] private IPersonService PersonService { get; set; } = default!;
    [Inject] private IAccountingService AccountingService { get; set; } = default!;
    [Inject] private IMeetingService MeetingService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private List<Person> _members = new();
    private List<Account> _incomeAccounts = new();
    private List<Meeting> _meetings = new();
    private CreatePaymentModel _model = new();

    private bool _isLoading = true;
    private bool _isSubmitting;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _members = (await PersonService.GetActiveAsync())
                .OrderBy(p => p.FullName)
                .ToList();

            _incomeAccounts = await AccountingService.GetAccountsByTypeAsync(AccountType.Income);

            _meetings = (await MeetingService.GetAllAsync())
                .Where(m => m.CostPerAttendee.HasValue && m.CostPerAttendee > 0)
                .OrderByDescending(m => m.Date)
                .Take(30)
                .ToList();
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

    private async Task HandleSubmit()
    {
        _errorMessage = string.Empty;

        if (string.IsNullOrEmpty(_model.MembershipNumber))
        {
            _errorMessage = "Please select a member.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_model.Reference))
        {
            _errorMessage = "Please enter a description.";
            return;
        }

        if (_model.Amount <= 0)
        {
            _errorMessage = "Please enter a valid amount.";
            return;
        }

        if (!_model.IncomeAccountId.HasValue)
        {
            _errorMessage = "Please select an income account.";
            return;
        }

        _isSubmitting = true;

        try
        {
            var payment = new Payment
            {
                MembershipNumber = _model.MembershipNumber,
                Amount = _model.Amount,
                PaymentType = PaymentType.Other,
                DueDate = _model.DueDate,
                Status = PaymentStatus.Pending,
                Reference = _model.Reference.Trim(),
                Notes = string.IsNullOrWhiteSpace(_model.Notes) ? null : _model.Notes.Trim(),
                IncomeAccountId = _model.IncomeAccountId,
                MeetingId = _model.MeetingId
            };

            var result = await PaymentService.CreateAsync(payment);

            if (result.Success)
            {
                NavigationManager.NavigateTo("/Payments?success=created");
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

    private class CreatePaymentModel
    {
        [Required(ErrorMessage = "Please select a member.")]
        public string MembershipNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a description.")]
        [MaxLength(200)]
        public string Reference { get; set; } = string.Empty;

        [Required]
        [Range(0.01, 10000, ErrorMessage = "Amount must be between £0.01 and £10,000.")]
        public decimal Amount { get; set; }

        [Required]
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(14);

        public string? Notes { get; set; }

        public int? IncomeAccountId { get; set; }

        public int? MeetingId { get; set; }
    }
}