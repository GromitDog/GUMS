using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Payments;

public partial class MemberBalances
{
    [Inject] private IPaymentService PaymentService { get; set; } = default!;
    [Inject] private ICreditService CreditService { get; set; } = default!;
    [Inject] private IPersonService PersonService { get; set; } = default!;

    private List<MemberBalanceRow> _rows = new();
    private List<MemberBalanceRow> _filteredRows = new();
    private bool _isLoading = true;
    private string _searchTerm = string.Empty;
    private string _filterMode = "owing"; // "all", "owing", "credit", "clear"
    private string _sortColumn = "Name";
    private bool _sortAscending = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;

        var girls = await PersonService.GetByTypeAsync(PersonType.Girl, activeOnly: true);
        var allPayments = await PaymentService.GetAllAsync();
        var membersWithCredit = await CreditService.GetMembersWithCreditAsync();

        var creditLookup = membersWithCredit.ToDictionary(c => c.MembershipNumber, c => c.CreditBalance);

        // Group payments by membership number
        var paymentsByMember = allPayments
            .GroupBy(p => p.MembershipNumber)
            .ToDictionary(g => g.Key, g => g.ToList());

        _rows = girls.Select(g =>
        {
            var payments = paymentsByMember.GetValueOrDefault(g.MembershipNumber, new List<Payment>());
            var pending = payments.Where(p => p.Status == PaymentStatus.Pending).ToList();
            var outstanding = pending.Sum(p => p.OutstandingBalance);
            var overdue = pending.Count(p => p.DueDate < DateTime.Today);
            var credit = creditLookup.GetValueOrDefault(g.MembershipNumber, 0m);

            return new MemberBalanceRow
            {
                PersonId = g.Id,
                MembershipNumber = g.MembershipNumber,
                Name = g.FullName ?? g.MembershipNumber,
                Outstanding = outstanding,
                OverdueCount = overdue,
                CreditBalance = credit,
                PendingCount = pending.Count,
                NetPosition = credit - outstanding
            };
        }).ToList();

        ApplyFilters();
        _isLoading = false;
    }

    private void ApplyFilters()
    {
        _filteredRows = _rows;

        // Apply filter mode
        _filteredRows = _filterMode switch
        {
            "owing" => _filteredRows.Where(r => r.Outstanding > 0).ToList(),
            "credit" => _filteredRows.Where(r => r.CreditBalance > 0).ToList(),
            "clear" => _filteredRows.Where(r => r.Outstanding == 0 && r.CreditBalance == 0).ToList(),
            _ => _filteredRows
        };

        // Apply search
        if (!string.IsNullOrWhiteSpace(_searchTerm))
        {
            var search = _searchTerm.ToLower();
            _filteredRows = _filteredRows
                .Where(r => r.Name.ToLower().Contains(search) || r.MembershipNumber.ToLower().Contains(search))
                .ToList();
        }

        // Apply sort
        _filteredRows = _sortColumn switch
        {
            "Name" => _sortAscending
                ? _filteredRows.OrderBy(r => r.Name).ToList()
                : _filteredRows.OrderByDescending(r => r.Name).ToList(),
            "Outstanding" => _sortAscending
                ? _filteredRows.OrderBy(r => r.Outstanding).ToList()
                : _filteredRows.OrderByDescending(r => r.Outstanding).ToList(),
            "Credit" => _sortAscending
                ? _filteredRows.OrderBy(r => r.CreditBalance).ToList()
                : _filteredRows.OrderByDescending(r => r.CreditBalance).ToList(),
            "Net" => _sortAscending
                ? _filteredRows.OrderBy(r => r.NetPosition).ToList()
                : _filteredRows.OrderByDescending(r => r.NetPosition).ToList(),
            _ => _filteredRows
        };
    }

    private void SetFilter(string mode)
    {
        _filterMode = mode;
        ApplyFilters();
    }

    private void SortBy(string column)
    {
        if (_sortColumn == column)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumn = column;
            _sortAscending = column == "Name"; // default asc for name, desc for amounts
        }
        ApplyFilters();
    }

    private string SortIcon(string column)
    {
        if (_sortColumn != column) return "bi-arrow-down-up text-muted";
        return _sortAscending ? "bi-sort-up" : "bi-sort-down";
    }

    private int CountOwing => _rows.Count(r => r.Outstanding > 0);
    private int CountWithCredit => _rows.Count(r => r.CreditBalance > 0);
    private int CountClear => _rows.Count(r => r.Outstanding == 0 && r.CreditBalance == 0);
    private decimal TotalOutstanding => _rows.Sum(r => r.Outstanding);
    private decimal TotalCredit => _rows.Sum(r => r.CreditBalance);

    public class MemberBalanceRow
    {
        public int PersonId { get; set; }
        public string MembershipNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Outstanding { get; set; }
        public int OverdueCount { get; set; }
        public decimal CreditBalance { get; set; }
        public int PendingCount { get; set; }
        public decimal NetPosition { get; set; }
    }
}
