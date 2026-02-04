using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Programme;

public partial class Badges
{
    [Inject] public required IBadgeService BadgeService { get; set; }
    [Inject] public required IProgrammeService ProgrammeService { get; set; }

    private List<BadgeDefinition> _badges = new();
    private BadgeDefinition _editingBadge = new();
    private List<BadgeClause> _editingClauses = new();

    private Theme? _filterTheme;
    private Section? _filterSection;
    private BadgeType? _filterType;

    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showBadgeModal;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadBadges();
    }

    private async Task LoadBadges()
    {
        _isLoading = true;
        try
        {
            _badges = await BadgeService.GetBadgesByFilterAsync(_filterTheme, _filterSection, _filterType);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading badges: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnThemeFilterChanged(ChangeEventArgs e)
    {
        _filterTheme = Enum.TryParse<Theme>(e.Value?.ToString(), out var theme) ? theme : null;
        await LoadBadges();
    }

    private async Task OnSectionFilterChanged(ChangeEventArgs e)
    {
        _filterSection = Enum.TryParse<Section>(e.Value?.ToString(), out var section) ? section : null;
        await LoadBadges();
    }

    private async Task OnTypeFilterChanged(ChangeEventArgs e)
    {
        _filterType = Enum.TryParse<BadgeType>(e.Value?.ToString(), out var type) ? type : null;
        await LoadBadges();
    }

    private void ShowAddBadge()
    {
        _editingBadge = new BadgeDefinition
        {
            RequiredCompletions = 4,
            BadgeType = BadgeType.SkillsBuilder
        };
        _editingClauses = new List<BadgeClause>();
        _showBadgeModal = true;
    }

    private void ShowEditBadge(BadgeDefinition badge)
    {
        _editingBadge = new BadgeDefinition
        {
            Id = badge.Id,
            Name = badge.Name,
            Theme = badge.Theme,
            BadgeType = badge.BadgeType,
            Section = badge.Section,
            Stage = badge.Stage,
            SkillsBuilderName = badge.SkillsBuilderName,
            RequiredCompletions = badge.RequiredCompletions
        };
        _editingClauses = badge.Clauses.Select(c => new BadgeClause
        {
            Id = c.Id,
            BadgeDefinitionId = c.BadgeDefinitionId,
            Name = c.Name,
            Description = c.Description,
            SortOrder = c.SortOrder
        }).ToList();
        _showBadgeModal = true;
    }

    private void CloseModal()
    {
        _showBadgeModal = false;
    }

    private void AddClause()
    {
        _editingClauses.Add(new BadgeClause
        {
            Name = string.Empty,
            SortOrder = _editingClauses.Count
        });
    }

    private void RemoveClause(int index)
    {
        _editingClauses.RemoveAt(index);
    }

    private async Task SaveBadge()
    {
        _isSaving = true;
        _errorMessage = string.Empty;

        try
        {
            if (_editingBadge.Id > 0)
            {
                var result = await BadgeService.UpdateBadgeAsync(_editingBadge);
                if (!result.Success)
                {
                    _errorMessage = result.ErrorMessage;
                    return;
                }

                // Sync clauses
                var existingClauses = await BadgeService.GetClausesForBadgeAsync(_editingBadge.Id);
                foreach (var existing in existingClauses)
                {
                    if (_editingClauses.All(c => c.Id != existing.Id))
                        await BadgeService.DeleteClauseAsync(existing.Id);
                }

                for (int i = 0; i < _editingClauses.Count; i++)
                {
                    var clause = _editingClauses[i];
                    clause.SortOrder = i;
                    if (string.IsNullOrWhiteSpace(clause.Name)) continue;

                    if (clause.Id > 0)
                    {
                        await BadgeService.UpdateClauseAsync(clause);
                    }
                    else
                    {
                        clause.BadgeDefinitionId = _editingBadge.Id;
                        await BadgeService.AddClauseAsync(clause);
                    }
                }

                _successMessage = "Badge updated successfully.";
            }
            else
            {
                _editingBadge.Clauses = _editingClauses
                    .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                    .ToList();

                var result = await BadgeService.CreateBadgeAsync(_editingBadge);
                if (!result.Success)
                {
                    _errorMessage = result.ErrorMessage;
                    return;
                }

                _successMessage = "Badge created successfully.";
            }

            _showBadgeModal = false;
            await LoadBadges();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error saving badge: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task DeleteBadge()
    {
        var result = await BadgeService.DeleteBadgeAsync(_editingBadge.Id);
        if (result.Success)
        {
            _successMessage = "Badge deleted.";
            _showBadgeModal = false;
            await LoadBadges();
        }
        else
        {
            _errorMessage = result.ErrorMessage;
        }
    }

    public static string GetThemeColor(Theme theme) => theme switch
    {
        Theme.KnowMyself => "#1A9FE0",
        Theme.ExpressMyself => "#E2187B",
        Theme.BeWell => "#904C94",
        Theme.HaveAdventures => "#79B741",
        Theme.TakeAction => "#F08E2C",
        Theme.SkillsForMyFuture => "#F3B6D1",
        _ => "#666"
    };
}
