using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Programme;

public partial class Umas
{
    [Inject] public required IBadgeService BadgeService { get; set; }
    [Inject] public required IProgrammeService ProgrammeService { get; set; }

    private List<UmaDefinition> _umas = new();
    private UmaDefinition _editingUma = new();
    private Theme? _filterTheme;

    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showUmaModal;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadUmas();
    }

    private async Task LoadUmas()
    {
        _isLoading = true;
        try
        {
            _umas = _filterTheme.HasValue
                ? await BadgeService.GetUmasByThemeAsync(_filterTheme.Value)
                : await BadgeService.GetAllUmasAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading UMAs: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnThemeFilterChanged(ChangeEventArgs e)
    {
        _filterTheme = Enum.TryParse<Theme>(e.Value?.ToString(), out var theme) ? theme : null;
        await LoadUmas();
    }

    private void ShowAddUma()
    {
        _editingUma = new UmaDefinition { Minutes = 30 };
        _showUmaModal = true;
    }

    private void ShowEditUma(UmaDefinition uma)
    {
        _editingUma = new UmaDefinition
        {
            Id = uma.Id,
            Name = uma.Name,
            Description = uma.Description,
            Theme = uma.Theme,
            Minutes = uma.Minutes
        };
        _showUmaModal = true;
    }

    private async Task SaveUma()
    {
        _isSaving = true;
        _errorMessage = string.Empty;

        try
        {
            if (_editingUma.Id > 0)
            {
                var result = await BadgeService.UpdateUmaAsync(_editingUma);
                if (!result.Success) { _errorMessage = result.ErrorMessage; return; }
                _successMessage = "UMA updated.";
            }
            else
            {
                var result = await BadgeService.CreateUmaAsync(_editingUma);
                if (!result.Success) { _errorMessage = result.ErrorMessage; return; }
                _successMessage = "UMA created.";
            }

            _showUmaModal = false;
            await LoadUmas();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error saving UMA: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task DeleteUma(int id)
    {
        var result = await BadgeService.DeleteUmaAsync(id);
        if (result.Success)
        {
            _successMessage = "UMA deleted.";
            await LoadUmas();
        }
        else
        {
            _errorMessage = result.ErrorMessage;
        }
    }
}
