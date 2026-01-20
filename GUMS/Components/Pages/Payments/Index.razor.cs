using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Payments;

public partial class Index
{
    [Inject] private IPaymentService PaymentService { get; set; } = default!;
    [Inject] private ITermService TermService { get; set; } = default!;
    [Inject] private IPersonService PersonService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private List<Payment> _allPayments = new();
    private List<Payment> _filteredPayments = new();
    private List<Term> _terms = new();
    private Dictionary<string, string> _memberNames = new();
    private PaymentDashboardStats _dashboardStats = new();

    private PaymentStatus? _selectedStatus;
    private int _selectedTermId;
    private string _searchTerm = string.Empty;

    private bool _isLoading = true;
    private bool _isCancelling;
    private bool _showCancelConfirm;
    private Payment? _paymentToCancel;
    private string _cancelReason = string.Empty;
    private string _successMessage = string.Empty;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        // Check for success message from navigation state
        var uri = new Uri(NavigationManager.Uri);
        if (uri.Query.Contains("success=recorded"))
        {
            _successMessage = "Payment recorded successfully!";
        }
        else if (uri.Query.Contains("success=generated"))
        {
            _successMessage = "Termly subscriptions generated successfully!";
        }
        else if (uri.Query.Contains("success=cancelled"))
        {
            _successMessage = "Payment cancelled successfully!";
        }

        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;

        try
        {
            _allPayments = await PaymentService.GetAllAsync();
            _terms = await TermService.GetAllAsync();
            _dashboardStats = await PaymentService.GetDashboardStatsAsync();

            // Load member names
            var persons = await PersonService.GetActiveAsync();
            _memberNames = persons.ToDictionary(
                p => p.MembershipNumber,
                p => p.FullName ?? p.MembershipNumber);

            ApplyFilters();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading payments: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OnStatusFilterChange(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        _selectedStatus = string.IsNullOrEmpty(value)
            ? null
            : Enum.Parse<PaymentStatus>(value);
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        _filteredPayments = _allPayments;

        // Filter by status
        if (_selectedStatus.HasValue)
        {
            _filteredPayments = _filteredPayments
                .Where(p => p.Status == _selectedStatus.Value)
                .ToList();
        }

        // Filter by term
        if (_selectedTermId > 0)
        {
            _filteredPayments = _filteredPayments
                .Where(p => p.TermId == _selectedTermId)
                .ToList();
        }

        // Filter by search term
        if (!string.IsNullOrWhiteSpace(_searchTerm))
        {
            var search = _searchTerm.ToLower();
            _filteredPayments = _filteredPayments
                .Where(p => p.MembershipNumber.ToLower().Contains(search) ||
                           _memberNames.GetValueOrDefault(p.MembershipNumber, "").ToLower().Contains(search) ||
                           p.Reference.ToLower().Contains(search))
                .ToList();
        }
    }

    private void ClearFilters()
    {
        _selectedStatus = null;
        _selectedTermId = 0;
        _searchTerm = string.Empty;
        ApplyFilters();
    }

    private void ClearSuccess()
    {
        _successMessage = string.Empty;
    }

    private void ClearError()
    {
        _errorMessage = string.Empty;
    }

    private void ShowCancelConfirm(Payment payment)
    {
        _paymentToCancel = payment;
        _cancelReason = string.Empty;
        _showCancelConfirm = true;
    }

    private void CancelCancelConfirm()
    {
        _paymentToCancel = null;
        _cancelReason = string.Empty;
        _showCancelConfirm = false;
    }

    private async Task CancelPayment()
    {
        if (_paymentToCancel == null || string.IsNullOrWhiteSpace(_cancelReason)) return;

        _isCancelling = true;
        _errorMessage = string.Empty;

        try
        {
            var result = await PaymentService.CancelPaymentAsync(_paymentToCancel.Id, _cancelReason);

            if (result.Success)
            {
                _successMessage = $"Payment '{_paymentToCancel.Reference}' cancelled successfully!";
                _showCancelConfirm = false;
                _paymentToCancel = null;
                _cancelReason = string.Empty;
                await LoadData();
            }
            else
            {
                _errorMessage = result.ErrorMessage;
                _showCancelConfirm = false;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"An error occurred: {ex.Message}";
            _showCancelConfirm = false;
        }
        finally
        {
            _isCancelling = false;
        }
    }
}
