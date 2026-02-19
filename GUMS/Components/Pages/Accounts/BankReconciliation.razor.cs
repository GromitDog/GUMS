using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class BankReconciliation
{
    [Inject] private IReconciliationService ReconciliationService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private Data.Entities.BankReconciliation? _currentReconciliation;
    private Data.Entities.BankReconciliation? _lastCompleted;
    private List<ReconciliationLineDto> _lines = new();
    private ReconciliationSummary _summary = new();
    private List<Data.Entities.BankReconciliation> _history = new();
    private decimal _currentBookBalance;

    // New reconciliation form
    private DateTime _statementDate = DateTime.Today;
    private decimal _statementBalance;
    private string? _notes;

    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showAbandonConfirm;
    private string _successMessage = string.Empty;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        try
        {
            _currentReconciliation = await ReconciliationService.GetCurrentReconciliationAsync();
            _lastCompleted = await ReconciliationService.GetLastCompletedReconciliationAsync();
            _currentBookBalance = await ReconciliationService.GetCurrentBankBookBalanceAsync();
            _history = await ReconciliationService.GetReconciliationHistoryAsync();

            if (_currentReconciliation != null)
            {
                await LoadReconciliationData();
            }
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

    private async Task LoadReconciliationData()
    {
        if (_currentReconciliation == null) return;

        _lines = await ReconciliationService.GetBankLinesForReconciliationAsync(_currentReconciliation.Id);
        _summary = await ReconciliationService.GetReconciliationSummaryAsync(_currentReconciliation.Id);
    }

    private async Task StartReconciliation()
    {
        _isSaving = true;
        _errorMessage = string.Empty;

        try
        {
            var result = await ReconciliationService.StartReconciliationAsync(
                _statementDate, _statementBalance, _notes);

            if (result.Success)
            {
                _currentReconciliation = result.Reconciliation;
                _successMessage = "Reconciliation started.";
                _notes = null;
                await LoadReconciliationData();
            }
            else
            {
                _errorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error starting reconciliation: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task ToggleLine(int transactionLineId)
    {
        if (_currentReconciliation == null) return;

        var result = await ReconciliationService.ToggleLineClearedAsync(
            _currentReconciliation.Id, transactionLineId);

        if (result.Success)
        {
            // Update local state for responsiveness
            var line = _lines.FirstOrDefault(l => l.TransactionLineId == transactionLineId);
            if (line != null)
                line.IsCleared = !line.IsCleared;

            _summary = await ReconciliationService.GetReconciliationSummaryAsync(_currentReconciliation.Id);
        }
        else
        {
            _errorMessage = result.ErrorMessage;
        }
    }

    private async Task CompleteReconciliation()
    {
        if (_currentReconciliation == null) return;

        _isSaving = true;
        _errorMessage = string.Empty;

        try
        {
            var result = await ReconciliationService.CompleteReconciliationAsync(_currentReconciliation.Id);

            if (result.Success)
            {
                _successMessage = "Reconciliation completed successfully!";
                _currentReconciliation = null;
                _showAbandonConfirm = false;
                await LoadData();
            }
            else
            {
                _errorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error completing reconciliation: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task AbandonReconciliation()
    {
        if (_currentReconciliation == null) return;

        _isSaving = true;
        _errorMessage = string.Empty;

        try
        {
            var result = await ReconciliationService.AbandonReconciliationAsync(_currentReconciliation.Id);

            if (result.Success)
            {
                _successMessage = "Reconciliation abandoned. All lines have been uncleared.";
                _currentReconciliation = null;
                _showAbandonConfirm = false;
                await LoadData();
            }
            else
            {
                _errorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error abandoning reconciliation: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void ClearSuccess() => _successMessage = string.Empty;
    private void ClearError() => _errorMessage = string.Empty;
}
