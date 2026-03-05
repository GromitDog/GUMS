using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Inventory;

public partial class EditStockItem
{
    [Inject] private IInventoryService InventoryService { get; set; } = default!;
    [Inject] private IBadgeService BadgeService { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    [Parameter] public int? Id { get; set; }

    private BadgeStockItem _item = new();
    private List<BadgeDefinition> _allBadges = new();
    private List<BadgeDefinition> _filteredBadges = new();
    private readonly int[] _nightsTiers = { 1, 2, 5, 10, 15, 20, 25, 30 };
    private bool _isNew;
    private bool _isLoading = true;
    private bool _isSaving;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        _isNew = !Id.HasValue;
        _allBadges = await BadgeService.GetAllBadgesAsync();

        if (_isNew)
        {
            _item = new BadgeStockItem { StockType = BadgeStockType.SkillsBuilder };
            FilterBadges();
        }
        else
        {
            var existing = await InventoryService.GetStockItemByIdAsync(Id!.Value);
            if (existing == null)
            {
                Nav.NavigateTo("/Inventory");
                return;
            }
            _item = new BadgeStockItem
            {
                Id = existing.Id,
                Name = existing.Name,
                StockType = existing.StockType,
                BadgeDefinitionId = existing.BadgeDefinitionId,
                ThemeAwardLevel = existing.ThemeAwardLevel,
                AwardTheme = existing.AwardTheme,
                NightsAwayTier = existing.NightsAwayTier,
                UnitCost = existing.UnitCost,
                ReorderThreshold = existing.ReorderThreshold,
                Notes = existing.Notes,
                IsActive = existing.IsActive
            };
            FilterBadges();
        }

        _isLoading = false;
    }

    private void OnStockTypeChanged()
    {
        _item.BadgeDefinitionId = null;
        _item.ThemeAwardLevel = null;
        _item.AwardTheme = null;
        _item.NightsAwayTier = null;
        FilterBadges();
    }

    private void OnAwardThemeChanged()
    {
        if (_item.AwardTheme.HasValue)
            _item.ThemeAwardLevel = null;
    }

    private void OnThemeAwardLevelChanged()
    {
        if (_item.ThemeAwardLevel.HasValue)
            _item.AwardTheme = null;
    }

    private void FilterBadges()
    {
        _filteredBadges = _item.StockType switch
        {
            BadgeStockType.SkillsBuilder => _allBadges.Where(b => b.BadgeType == BadgeType.SkillsBuilder).OrderBy(b => b.Name).ToList(),
            BadgeStockType.InterestBadge => _allBadges.Where(b => b.BadgeType == BadgeType.InterestBadge).OrderBy(b => b.Name).ToList(),
            BadgeStockType.FunBadge => _allBadges.Where(b => b.BadgeType == BadgeType.FunBadge).OrderBy(b => b.Name).ToList(),
            _ => new()
        };
    }

    private async Task Save()
    {
        _isSaving = true;
        _errorMessage = string.Empty;

        try
        {
            if (_isNew)
            {
                var (success, error, _) = await InventoryService.CreateStockItemAsync(_item);
                if (!success) { _errorMessage = error; return; }
            }
            else
            {
                var (success, error) = await InventoryService.UpdateStockItemAsync(_item);
                if (!success) { _errorMessage = error; return; }
            }

            Nav.NavigateTo("/Inventory");
        }
        finally
        {
            _isSaving = false;
        }
    }
}
