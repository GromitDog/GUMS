using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Components.Pages.Accounts;

public partial class EventBudget
{
    [Inject] private IBudgetService BudgetService { get; set; } = default!;
    [Inject] private IMeetingService MeetingService { get; set; } = default!;
    [Inject] private ApplicationDbContext DbContext { get; set; } = default!;

    [Parameter] public int MeetingId { get; set; }

    private Meeting? _meeting;
    private Data.Entities.EventBudget? _budget;
    private BudgetEstimate? _estimate;
    private List<Account> _expenseAccounts = new();
    private bool _isLoading = true;
    private string _successMessage = string.Empty;
    private string _errorMessage = string.Empty;

    // Item form state
    private bool _showItemForm;
    private int? _editingItemId;
    private string _itemDescription = string.Empty;
    private BudgetCostType _itemCostType = BudgetCostType.PerGirl;
    private decimal _itemAmount;
    private BudgetCostStatus _itemCostStatus = BudgetCostStatus.Estimate;
    private int? _itemExpenseAccountId;
    private string _budgetNotes = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        try
        {
            _meeting = await DbContext.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == MeetingId);
            _budget = await BudgetService.GetBudgetForMeetingAsync(MeetingId);
            _expenseAccounts = await DbContext.Accounts
                .AsNoTracking()
                .Where(a => a.Type == AccountType.Expense)
                .OrderBy(a => a.Name)
                .ToListAsync();

            if (_budget != null)
            {
                _budgetNotes = _budget.Notes ?? string.Empty;
                _estimate = await BudgetService.GetBudgetEstimateAsync(MeetingId);
            }
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

    private async Task CreateBudget()
    {
        var result = await BudgetService.CreateBudgetAsync(MeetingId, null);
        if (result.Success)
        {
            _successMessage = "Budget created.";
            await LoadData();
        }
        else
        {
            _errorMessage = result.ErrorMessage;
        }
    }

    private void ShowAddItem()
    {
        _editingItemId = null;
        _itemDescription = string.Empty;
        _itemCostType = BudgetCostType.PerGirl;
        _itemAmount = 0;
        _itemCostStatus = BudgetCostStatus.Estimate;
        _itemExpenseAccountId = null;
        _showItemForm = true;
    }

    private void ShowEditItem(EventBudgetItem item)
    {
        _editingItemId = item.Id;
        _itemDescription = item.Description;
        _itemCostType = item.CostType;
        _itemAmount = item.Amount;
        _itemCostStatus = item.CostStatus;
        _itemExpenseAccountId = item.ExpenseAccountId;
        _showItemForm = true;
    }

    private void CancelItemForm()
    {
        _showItemForm = false;
        _editingItemId = null;
    }

    private async Task SaveItem()
    {
        if (string.IsNullOrWhiteSpace(_itemDescription))
        {
            _errorMessage = "Description is required.";
            return;
        }

        if (_editingItemId.HasValue)
        {
            var item = new EventBudgetItem
            {
                Id = _editingItemId.Value,
                Description = _itemDescription,
                CostType = _itemCostType,
                Amount = _itemAmount,
                CostStatus = _itemCostStatus,
                ExpenseAccountId = _itemExpenseAccountId
            };
            var result = await BudgetService.UpdateBudgetItemAsync(item);
            if (!result.Success)
            {
                _errorMessage = result.ErrorMessage;
                return;
            }
            _successMessage = "Item updated.";
        }
        else
        {
            var item = new EventBudgetItem
            {
                EventBudgetId = _budget!.Id,
                Description = _itemDescription,
                CostType = _itemCostType,
                Amount = _itemAmount,
                CostStatus = _itemCostStatus,
                ExpenseAccountId = _itemExpenseAccountId
            };
            var result = await BudgetService.AddBudgetItemAsync(item);
            if (!result.Success)
            {
                _errorMessage = result.ErrorMessage;
                return;
            }
            _successMessage = "Item added.";
        }

        _showItemForm = false;
        _editingItemId = null;
        await LoadData();
    }

    private async Task DeleteItem(int itemId)
    {
        var result = await BudgetService.DeleteBudgetItemAsync(itemId);
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

    private async Task SaveNotes()
    {
        if (_budget == null) return;

        var tracked = await DbContext.EventBudgets.FindAsync(_budget.Id);
        if (tracked != null)
        {
            tracked.Notes = _budgetNotes;
            tracked.LastModifiedDate = DateTime.UtcNow;
            await DbContext.SaveChangesAsync();
            _successMessage = "Notes saved.";
        }
    }

    private static string GetCostTypeBadgeClass(BudgetCostType costType) => costType switch
    {
        BudgetCostType.PerGirl => "bg-info",
        BudgetCostType.PerAdult => "bg-primary",
        BudgetCostType.PerPerson => "bg-success",
        BudgetCostType.FixedTotal => "bg-secondary",
        _ => "bg-secondary"
    };

    private static string GetCostTypeLabel(BudgetCostType costType) => costType switch
    {
        BudgetCostType.PerGirl => "Per Girl",
        BudgetCostType.PerAdult => "Per Adult",
        BudgetCostType.PerPerson => "Per Person",
        BudgetCostType.FixedTotal => "Fixed",
        _ => costType.ToString()
    };
}
