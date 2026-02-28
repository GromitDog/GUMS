using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Inventory;

public partial class RecordTransaction
{
    [Inject] private IInventoryService InventoryService { get; set; } = default!;
    [Inject] private IAccountingService AccountingService { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    [Parameter] public int StockItemId { get; set; }

    private BadgeStockItem? _item;
    private StockTransactionType _txnType = StockTransactionType.Purchase;
    private DateTime _date = DateTime.Today;
    private int _quantity = 1;
    private decimal? _unitCost;
    private string _notes = string.Empty;

    // Expense claim fields
    private bool _createExpenseClaim;
    private int _expenseAccountId;
    private int _addToClaimId;       // 0 = create new
    private string _claimedBy = string.Empty;

    private List<Account> _expenseAccounts = new();
    private List<ExpenseClaim> _draftClaims = new();

    private bool _isLoading = true;
    private bool _isSaving;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        _item = await InventoryService.GetStockItemByIdAsync(StockItemId);
        if (_item != null)
            _unitCost = _item.UnitCost;

        _expenseAccounts = await AccountingService.GetExpenseAccountsAsync();
        _draftClaims = await AccountingService.GetExpenseClaimsAsync(ExpenseClaimStatus.Draft);

        _isLoading = false;
    }

    private void OnTypeChanged()
    {
        _quantity = 1;
        _createExpenseClaim = false;
        _errorMessage = string.Empty;
    }

    private decimal? _effectiveCost => _unitCost ?? _item?.UnitCost;
    private bool _canCreateClaim => _effectiveCost.HasValue && _effectiveCost > 0;

    private bool _notesRequired => _txnType is StockTransactionType.ManualIn
        or StockTransactionType.ManualOut
        or StockTransactionType.VisitorAward
        or StockTransactionType.Adjustment;

    private string QuantityHint => _txnType switch
    {
        StockTransactionType.Purchase => "units received",
        StockTransactionType.ManualIn => "units added",
        StockTransactionType.ManualOut or StockTransactionType.VisitorAward => "units removed",
        StockTransactionType.Adjustment => "positive to add, negative to remove",
        _ => ""
    };

    private string NotesPlaceholder => _txnType switch
    {
        StockTransactionType.VisitorAward => "e.g. Visitor from Brownies, 14 Feb",
        StockTransactionType.ManualOut => "e.g. Badge lost/damaged",
        StockTransactionType.ManualIn => "e.g. Returned unused badge",
        StockTransactionType.Adjustment => "e.g. Stocktake — found 3 extra",
        _ => ""
    };

    private async Task Save()
    {
        _isSaving = true;
        _errorMessage = string.Empty;

        try
        {
            if (_txnType == StockTransactionType.Purchase)
            {
                await SavePurchase();
            }
            else
            {
                await SaveOtherTransaction();
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task SavePurchase()
    {
        // Validate expense claim fields if requested
        PurchaseExpenseOptions? expenseOptions = null;
        if (_createExpenseClaim)
        {
            if (_expenseAccountId == 0)
            {
                _errorMessage = "Please select an expense account.";
                return;
            }
            if (_addToClaimId == 0 && string.IsNullOrWhiteSpace(_claimedBy))
            {
                _errorMessage = "Please enter the name of the person claiming.";
                return;
            }

            expenseOptions = new PurchaseExpenseOptions
            {
                ExpenseAccountId = _expenseAccountId,
                AddToClaimId = _addToClaimId == 0 ? null : _addToClaimId,
                ClaimedBy = _claimedBy.Trim()
            };
        }

        var txn = new BadgeStockTransaction
        {
            BadgeStockItemId = StockItemId,
            TransactionDate = _date,
            Quantity = Math.Abs(_quantity),
            TransactionType = StockTransactionType.Purchase,
            UnitCost = _unitCost,
            Notes = string.IsNullOrWhiteSpace(_notes) ? null : _notes.Trim()
        };

        var (success, error) = await InventoryService.RecordPurchaseAsync(txn, expenseOptions);
        if (!success) { _errorMessage = error; return; }

        Nav.NavigateTo($"/Inventory/Stock/{StockItemId}");
    }

    private async Task SaveOtherTransaction()
    {
        var signedQty = _txnType switch
        {
            StockTransactionType.ManualIn => Math.Abs(_quantity),
            StockTransactionType.ManualOut or StockTransactionType.VisitorAward => -Math.Abs(_quantity),
            StockTransactionType.Adjustment => _quantity,
            _ => _quantity
        };

        var txn = new BadgeStockTransaction
        {
            BadgeStockItemId = StockItemId,
            TransactionDate = _date,
            Quantity = signedQty,
            TransactionType = _txnType,
            Notes = string.IsNullOrWhiteSpace(_notes) ? null : _notes.Trim()
        };

        var (success, error) = await InventoryService.RecordTransactionAsync(txn);
        if (!success) { _errorMessage = error; return; }

        Nav.NavigateTo($"/Inventory/Stock/{StockItemId}");
    }
}
