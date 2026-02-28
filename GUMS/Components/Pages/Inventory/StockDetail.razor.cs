using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Inventory;

public partial class StockDetail
{
    [Inject] private IInventoryService InventoryService { get; set; } = default!;

    [Parameter] public int Id { get; set; }

    private BadgeStockItem? _item;
    private List<BadgeStockTransaction> _transactions = new();
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        _item = await InventoryService.GetStockItemByIdAsync(Id);
        if (_item != null)
            _transactions = await InventoryService.GetTransactionsForItemAsync(Id);
        _isLoading = false;
    }

    private string StockColour => _item switch
    {
        { CurrentQuantity: 0 } => "var(--gg-error)",
        { ReorderThreshold: not null } i when i.CurrentQuantity < i.ReorderThreshold => "#f0ad4e",
        _ => "#198754"
    };

    private static string StockTypeLabel(BadgeStockType t) => t switch
    {
        BadgeStockType.SkillsBuilder => "Skills Builder",
        BadgeStockType.InterestBadge => "Interest Badge",
        BadgeStockType.FunBadge => "Fun Badge",
        BadgeStockType.ThemeAward => "Theme Award",
        BadgeStockType.NightsAway => "Nights Away",
        BadgeStockType.Other => "Other",
        _ => t.ToString()
    };

    private static string TxnLabel(StockTransactionType t) => t switch
    {
        StockTransactionType.Purchase => "Purchase",
        StockTransactionType.Award => "Award",
        StockTransactionType.VisitorAward => "Visitor Award",
        StockTransactionType.ManualIn => "Manual In",
        StockTransactionType.ManualOut => "Manual Out",
        StockTransactionType.Adjustment => "Adjustment",
        _ => t.ToString()
    };

    private static string TxnBadgeClass(StockTransactionType t) => t switch
    {
        StockTransactionType.Purchase => "bg-success",
        StockTransactionType.Award or StockTransactionType.VisitorAward => "bg-primary",
        StockTransactionType.ManualIn => "bg-info text-dark",
        StockTransactionType.ManualOut => "bg-warning text-dark",
        StockTransactionType.Adjustment => "bg-secondary",
        _ => "bg-secondary"
    };
}
