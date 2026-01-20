using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Payments;

public partial class Overdue
{
    [Inject] private IPaymentService PaymentService { get; set; } = default!;
    [Inject] private IPersonService PersonService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private List<Payment> _overduePayments = new();
    private Dictionary<string, List<Payment>> _paymentsByMember = new();
    private Dictionary<string, string> _memberNames = new();
    private decimal _totalOverdueAmount;
    private int _membersWithOverdue;

    private bool _isLoading = true;
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

        await LoadOverduePayments();
    }

    private async Task LoadOverduePayments()
    {
        _isLoading = true;

        try
        {
            _overduePayments = await PaymentService.GetOverduePaymentsAsync();

            // Group by member
            _paymentsByMember = _overduePayments
                .GroupBy(p => p.MembershipNumber)
                .OrderByDescending(g => g.Sum(p => p.OutstandingBalance))
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.DueDate).ToList());

            // Load member names
            var persons = await PersonService.GetActiveAsync();
            _memberNames = persons.ToDictionary(
                p => p.MembershipNumber,
                p => p.FullName ?? p.MembershipNumber);

            // Calculate totals
            _totalOverdueAmount = _overduePayments.Sum(p => p.OutstandingBalance);
            _membersWithOverdue = _paymentsByMember.Count;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading overdue payments: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ClearSuccess()
    {
        _successMessage = string.Empty;
    }

    private void ClearError()
    {
        _errorMessage = string.Empty;
    }
}
