using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Inventory;

public partial class Index
{
    [Inject] private IInventoryService InventoryService { get; set; } = default!;

    private List<BadgeStockItem> _items = new();
    private List<LowStockAlert> _alerts = new();
    private IEnumerable<IGrouping<string, BadgeStockItem>> _groups = Enumerable.Empty<IGrouping<string, BadgeStockItem>>();
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        _items = await InventoryService.GetAllStockItemsAsync();
        _alerts = await InventoryService.GetLowStockAlertsAsync();
        _groups = _items.GroupBy(i => StockTypeLabel(i.StockType));
        _isLoading = false;
    }

    private static string StockTypeLabel(BadgeStockType t) => t switch
    {
        BadgeStockType.SkillsBuilder => "Skills Builder Badges",
        BadgeStockType.InterestBadge => "Interest Badges",
        BadgeStockType.FunBadge => "Fun Badges",
        BadgeStockType.ThemeAward => "Theme Award Badges",
        BadgeStockType.NightsAway => "Nights Away Badges",
        BadgeStockType.Other => "Other",
        _ => t.ToString()
    };
}
