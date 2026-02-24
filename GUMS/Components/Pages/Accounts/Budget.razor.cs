using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class Budget
{
    [Inject] private IUnitBudgetService UnitBudgetService { get; set; } = default!;
    [Inject] private IAccountingService AccountingService { get; set; } = default!;
    [Inject] private IConfigurationService ConfigurationService { get; set; } = default!;

    private UnitBudgetSummary? _summary;
    private UnitBudget? _budget;
    private List<Account> _expenseAccounts = new();
    private List<DateTime> _availableYearEnds = new();
    private DateTime _selectedYearEnd;

    private UnitBudgetItem _newItem = new();
    private int? _editingItemId;
    private UnitBudgetItem _editItem = new();

    private bool _isLoading = true;
    private bool _isSaving;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;

    private bool _showAddForm;

    protected override async Task OnInitializedAsync()
    {
        var config = await ConfigurationService.GetConfigurationAsync();
        _availableYearEnds = GenerateAvailableYearEnds(config);
        _selectedYearEnd = _availableYearEnds.FirstOrDefault(d => d <= DateTime.Today);

        _expenseAccounts = await AccountingService.GetExpenseAccountsAsync();

        await LoadData();
    }

    private async Task OnYearChanged(ChangeEventArgs e)
    {
        if (DateTime.TryParse(e.Value?.ToString(), out var date))
        {
            _selectedYearEnd = date;
            CancelForms();
            await LoadData();
        }
    }

    private async Task LoadData()
    {
        _isLoading = true;
        _errorMessage = string.Empty;
        try
        {
            _summary = await UnitBudgetService.GetBudgetSummaryAsync(_selectedYearEnd);
            _budget = await UnitBudgetService.GetBudgetForYearAsync(_selectedYearEnd);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading budget: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ShowAddForm()
    {
        _newItem = new UnitBudgetItem { Frequency = BudgetFrequency.Yearly, Allocation = BudgetAllocation.Fixed };
        _editingItemId = null;
        _showAddForm = true;
    }

    private void ShowEditItem(UnitBudgetItem item)
    {
        _editingItemId = item.Id;
        _editItem = new UnitBudgetItem
        {
            Id = item.Id,
            UnitBudgetId = item.UnitBudgetId,
            Description = item.Description,
            Frequency = item.Frequency,
            Allocation = item.Allocation,
            Amount = item.Amount,
            ExpenseAccountId = item.ExpenseAccountId,
            Notes = item.Notes,
            SortOrder = item.SortOrder
        };
        _showAddForm = false;
    }

    private void CancelForms()
    {
        _showAddForm = false;
        _editingItemId = null;
    }

    private async Task SaveNewItem()
    {
        _isSaving = true;
        _errorMessage = string.Empty;
        try
        {
            // Ensure budget exists for this year
            _budget = await UnitBudgetService.GetOrCreateBudgetForYearAsync(_selectedYearEnd);
            _newItem.UnitBudgetId = _budget.Id;

            var result = await UnitBudgetService.AddItemAsync(_newItem);
            if (result.Success)
            {
                _successMessage = "Item added.";
                _showAddForm = false;
                await LoadData();
            }
            else
            {
                _errorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error saving item: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task SaveEditItem()
    {
        _isSaving = true;
        _errorMessage = string.Empty;
        try
        {
            var result = await UnitBudgetService.UpdateItemAsync(_editItem);
            if (result.Success)
            {
                _successMessage = "Item updated.";
                _editingItemId = null;
                await LoadData();
            }
            else
            {
                _errorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error updating item: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task DeleteItem(int itemId)
    {
        _isSaving = true;
        _errorMessage = string.Empty;
        try
        {
            var result = await UnitBudgetService.DeleteItemAsync(itemId);
            if (result.Success)
            {
                _successMessage = "Item deleted.";
                await LoadData();
            }
            else
            {
                _errorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error deleting item: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private static List<DateTime> GenerateAvailableYearEnds(Data.Entities.UnitConfiguration config)
    {
        var ends = new List<DateTime>();
        var today = DateTime.Today;
        for (var offset = -5; offset <= 1; offset++)
        {
            try
            {
                ends.Add(new DateTime(today.Year + offset, config.FinancialYearEndMonth, config.FinancialYearEndDay));
            }
            catch { /* skip invalid dates */ }
        }
        return ends.OrderByDescending(d => d).ToList();
    }

    private static string FrequencyLabel(BudgetFrequency f) => f switch
    {
        BudgetFrequency.Yearly => "Yearly",
        BudgetFrequency.Termly => "Per term",
        BudgetFrequency.Weekly => "Per week",
        _ => f.ToString()
    };

    private static string AllocationLabel(BudgetAllocation a) => a switch
    {
        BudgetAllocation.Fixed => "Fixed",
        BudgetAllocation.PerGirl => "Per girl",
        BudgetAllocation.PerPerson => "Per person",
        _ => a.ToString()
    };

    private int? NullableInt(string? value) =>
        int.TryParse(value, out var i) ? i : null;
}
