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
    private decimal _totalCashPaid;
    private decimal _totalCreditApplied;
    private decimal _totalRefundedToCredit;
    private decimal _totalCashRefund;
    private decimal _totalOutstanding;

    protected override async Task OnInitializedAsync()
    {
        var config = await ConfigurationService.GetConfigurationAsync();
        _unitName = config.UnitName;

        var person = await PersonService.GetByMembershipNumberAsync(MembershipNumber);
        _memberName = person?.FullName ?? MembershipNumber;

        _payments = await PaymentService.GetByMembershipNumberAsync(MembershipNumber);
        _creditBalance = await CreditService.GetCreditBalanceAsync(MembershipNumber);

        await BuildStatement();
        _isLoading = false;
    }

    private async Task BuildStatement()
    {
        _lines.Clear();

        // Identify which refunds went into the credit pool (vs handed back as cash).
        // Credit deposits sourced from a payment tell us how much of that payment's
        // RefundAmount was moved to credit rather than returned as cash.
        var creditHistory = await CreditService.GetCreditHistoryAsync(MembershipNumber);
        var refundedToCreditByPayment = creditHistory
            .Where(ct => ct.Type == CreditTransactionType.Deposit && ct.SourcePaymentId != null)
            .GroupBy(ct => ct.SourcePaymentId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(ct => ct.Amount));

        foreach (var p in _payments
                     .Where(p => p.Status != PaymentStatus.Cancelled)
                     .OrderBy(p => p.DueDate))
        {
            var refundedToCredit = refundedToCreditByPayment.GetValueOrDefault(p.Id, 0m);
            var cashRefund = Math.Max(0m, p.RefundAmount - refundedToCredit);
            // Cash paid = what the parent actually paid in money toward this line
            // (excludes credit applied, which is tracked separately).
            var cashPaid = p.AmountPaid - p.CreditApplied;

            // Only Pending lines contribute to running balance. Paid/Refunded lines
            // are settled — a refund-to-credit is not "owed back" because the money
            // is held in the credit pool (consumed by other lines as CreditApplied,
            // or shown as the footer Credit held figure). Cash refunds zero out too —
            // the parent received the money, cancelling what they paid.
            var outstanding = p.Status == PaymentStatus.Pending ? p.OutstandingBalance : 0m;

            _lines.Add(new StatementLine
            {
                Date = p.RefundDate ?? p.PaymentDate ?? p.DueDate,
                Description = p.Reference,
                Status = p.Status switch
                {
                    PaymentStatus.Refunded => "Refunded",
                    PaymentStatus.Paid => "Paid",
                    _ => "Due"
                },
                Charged = p.Amount,
                CashPaid = cashPaid,
                CreditApplied = p.CreditApplied,
                RefundedToCredit = refundedToCredit,
                CashRefund = cashRefund,
                Outstanding = outstanding
            });
        }

        decimal running = 0;
        foreach (var line in _lines)
        {
            running += line.Outstanding;
            line.Balance = running;
        }

        _totalCharged = _lines.Sum(l => l.Charged);
        _totalCashPaid = _lines.Sum(l => l.CashPaid);
        _totalCreditApplied = _lines.Sum(l => l.CreditApplied);
        _totalRefundedToCredit = _lines.Sum(l => l.RefundedToCredit);
        _totalCashRefund = _lines.Sum(l => l.CashRefund);
        _totalOutstanding = _lines.Sum(l => l.Outstanding);
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
        public decimal CashPaid { get; set; }
        public decimal CreditApplied { get; set; }
        public decimal RefundedToCredit { get; set; }
        public decimal CashRefund { get; set; }
        public decimal Outstanding { get; set; }
        public decimal Balance { get; set; }
    }
}
