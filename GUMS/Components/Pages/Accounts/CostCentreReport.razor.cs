using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class CostCentreReport
{
    [Inject] private IAccountingService AccountingService { get; set; } = default!;
    [Inject] private IConfigurationService ConfigurationService { get; set; } = default!;

    private CostCentreReportData? _report;
    private DateTime _dateFrom;
    private DateTime _dateTo;
    private bool _isLoading = true;
    private HashSet<int?> _expandedRows = new();

    // Use the inner type alias to avoid conflict with the page class name
    private class CostCentreReportData
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public List<CostCentreReportLine> Lines { get; set; } = new();
        public decimal TotalIncome => Lines.Sum(l => l.Income);
        public decimal TotalExpenses => Lines.Sum(l => l.Expenses);
        public decimal TotalNet => TotalIncome - TotalExpenses;
    }

    protected override async Task OnInitializedAsync()
    {
        // Default to current financial year (Sept to Aug for Girlguiding)
        var config = await ConfigurationService.GetConfigurationAsync();
        var today = DateTime.Today;
        var yearStart = today.Month >= 9
            ? new DateTime(today.Year, 9, 1)
            : new DateTime(today.Year - 1, 9, 1);

        _dateFrom = yearStart;
        _dateTo = today;

        await LoadReport();
    }

    private async Task LoadReport()
    {
        _isLoading = true;
        try
        {
            var result = await AccountingService.GetCostCentreReportAsync(_dateFrom, _dateTo);
            _report = new CostCentreReportData
            {
                DateFrom = result.DateFrom,
                DateTo = result.DateTo,
                Lines = result.Lines
            };
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ToggleExpand(int? costCentreId)
    {
        if (!_expandedRows.Remove(costCentreId))
            _expandedRows.Add(costCentreId);
    }

    private bool IsExpanded(int? costCentreId) => _expandedRows.Contains(costCentreId);
}
