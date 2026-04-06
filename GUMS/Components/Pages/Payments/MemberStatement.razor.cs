using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GUMS.Components.Pages.Payments;

public partial class MemberStatement
{
    [Parameter] public string MembershipNumber { get; set; } = string.Empty;

    [Inject] private IPaymentService PaymentService { get; set; } = default!;
    [Inject] private ICreditService CreditService { get; set; } = default!;
    [Inject] private IPersonService PersonService { get; set; } = default!;
    [Inject] private IConfigurationService ConfigurationService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private string _unitName = string.Empty;
    private string _memberName = string.Empty;
    private List<Payment> _payments = new();
    private decimal _creditBalance;
    private bool _isLoading = true;

    private List<StatementLine> _lines = new();
    private decimal _totalCharged;
    private decimal _totalPaid;
    private decimal _totalRefundCredit;
    private decimal _totalOutstanding;

    protected override async Task OnInitializedAsync()
    {
        var config = await ConfigurationService.GetConfigurationAsync();
        _unitName = config.UnitName;

        var person = await PersonService.GetByMembershipNumberAsync(MembershipNumber);
        _memberName = person?.FullName ?? MembershipNumber;

        _payments = await PaymentService.GetByMembershipNumberAsync(MembershipNumber);
        _creditBalance = await CreditService.GetCreditBalanceAsync(MembershipNumber);

        BuildStatement();
        _isLoading = false;
    }

    private void BuildStatement()
    {
        _lines.Clear();

        // From a parent's perspective:
        // - Pending/Paid payments = they were charged and (maybe) paid
        // - Refunded payments that were converted to credit = refund, but credit was used elsewhere
        // - Cash refunds = money back
        // Keep it simple: Charged | Paid | Balance

        foreach (var p in _payments
                     .Where(p => p.Status != PaymentStatus.Cancelled)
                     .OrderBy(p => p.DueDate))
        {
            if (p.Status == PaymentStatus.Refunded)
            {
                // Show the original charge and payment, then the refund/credit amount
                _lines.Add(new StatementLine
                {
                    Date = p.RefundDate ?? p.PaymentDate ?? p.DueDate,
                    Description = p.Reference,
                    Status = "Refunded",
                    Charged = p.Amount,
                    Paid = p.AmountPaid,
                    RefundCredit = p.RefundAmount
                });
            }
            else
            {
                // Pending or Paid — paid includes cash + credit applied (parent doesn't distinguish)
                _lines.Add(new StatementLine
                {
                    Date = p.PaymentDate ?? p.DueDate,
                    Description = p.Reference,
                    Status = p.Status == PaymentStatus.Paid ? "Paid" : "Due",
                    Charged = p.Amount,
                    Paid = p.AmountPaid
                });
            }
        }

        // Calculate running balance: charged - paid - refunded
        decimal running = 0;
        foreach (var line in _lines)
        {
            running += line.Charged - line.Paid - line.RefundCredit;
            line.Balance = running;
        }

        _totalCharged = _lines.Sum(l => l.Charged);
        _totalPaid = _lines.Sum(l => l.Paid);
        _totalRefundCredit = _lines.Sum(l => l.RefundCredit);
        _totalOutstanding = _lines.Where(l => l.Status == "Due").Sum(l => l.Charged - l.Paid);
    }

    private async Task Print()
    {
        await JS.InvokeVoidAsync("window.print");
    }

    public class StatementLine
    {
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Charged { get; set; }
        public decimal Paid { get; set; }
        public decimal RefundCredit { get; set; }
        public decimal Balance { get; set; }
    }
}
