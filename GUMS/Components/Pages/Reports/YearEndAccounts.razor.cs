using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Reports;

public partial class YearEndAccounts
{
    [Inject] private IAccountingService AccountingService { get; set; } = default!;
    [Inject] private IConfigurationService ConfigurationService { get; set; } = default!;

    private UnitConfiguration? _config;
    private List<DateTime> _availableYearEnds = new();
    private DateTime _selectedYearEnd;
    private YearEndAccountsReport? _report;
    private bool _isLoading = true;
    private string _errorMessage = string.Empty;

    // Finalise modal state
    private YearClosingPreview? _preview;
    private bool _showModal1;
    private bool _showModal2;
    private bool _isFinalising;
    private string _finaliseError = string.Empty;
    private string _finaliseSuccess = string.Empty;

    private bool IsAlreadyFinalised =>
        _config?.AccountsLockedUntil.HasValue == true
        && _config.AccountsLockedUntil.Value >= _selectedYearEnd;

    private bool CanFinalise =>
        !IsAlreadyFinalised
        && _selectedYearEnd <= DateTime.Today;

    protected override async Task OnInitializedAsync()
    {
        _config = await ConfigurationService.GetConfigurationAsync();
        _availableYearEnds = GenerateAvailableYearEnds();
        _selectedYearEnd = _availableYearEnds.FirstOrDefault(d => d <= DateTime.Today);
        await LoadReport();
    }

    private async Task OnYearChanged(ChangeEventArgs e)
    {
        if (DateTime.TryParse(e.Value?.ToString(), out var date))
        {
            _selectedYearEnd = date;
            _finaliseError = string.Empty;
            _finaliseSuccess = string.Empty;
            await LoadReport();
        }
    }

    private async Task LoadReport()
    {
        _isLoading = true;
        _errorMessage = string.Empty;
        try
        {
            _report = await AccountingService.GetYearEndReportAsync(_selectedYearEnd);
            // Refresh config so the locked badge reflects the latest state
            _config = await ConfigurationService.GetConfigurationAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading report: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OpenFinaliseModal()
    {
        _finaliseError = string.Empty;
        _finaliseSuccess = string.Empty;
        _preview = await AccountingService.GetYearClosingPreviewAsync(_selectedYearEnd);
        _showModal1 = true;
        _showModal2 = false;
    }

    private void ProceedToJournal()
    {
        _showModal1 = false;
        _showModal2 = true;
    }

    private void BackToModal1()
    {
        _showModal2 = false;
        _showModal1 = true;
    }

    private void CloseModals()
    {
        _showModal1 = false;
        _showModal2 = false;
    }

    private async Task ConfirmFinalise()
    {
        _isFinalising = true;
        _finaliseError = string.Empty;
        try
        {
            var result = await AccountingService.FinaliseYearEndAsync(_selectedYearEnd);
            if (result.Success)
            {
                _showModal1 = false;
                _showModal2 = false;
                _finaliseSuccess = $"Year end {_selectedYearEnd:d MMMM yyyy} has been finalised and the period is now locked.";
                await LoadReport();
            }
            else
            {
                _finaliseError = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _finaliseError = $"An error occurred: {ex.Message}";
        }
        finally
        {
            _isFinalising = false;
        }
    }

    private List<DateTime> GenerateAvailableYearEnds()
    {
        if (_config == null) return new List<DateTime>();

        var ends = new List<DateTime>();
        var today = DateTime.Today;

        for (var offset = -5; offset <= 1; offset++)
        {
            try
            {
                ends.Add(new DateTime(today.Year + offset,
                    _config.FinancialYearEndMonth,
                    _config.FinancialYearEndDay));
            }
            catch { /* skip invalid dates */ }
        }

        return ends.OrderByDescending(d => d).ToList();
    }

    private static string FormatMoney(decimal? amount)
    {
        if (amount == null) return string.Empty;
        return amount < 0
            ? $"-£{Math.Abs(amount.Value):N2}"
            : $"£{amount.Value:N2}";
    }

    private static string FormatDate(DateTime d) =>
        d.ToString("d MMMM yyyy");

    private static string YearLabel(DateTime yearEnd)
    {
        var start = yearEnd.AddYears(-1).AddDays(1);
        return $"{start.ToString("d MMM yyyy")} to {yearEnd.ToString("d MMM yyyy")}";
    }

    private static string AmountClass(decimal? amount) =>
        amount < 0 ? "amt-negative" : string.Empty;
}
