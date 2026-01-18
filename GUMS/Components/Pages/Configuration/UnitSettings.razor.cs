using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Configuration;

[Authorize]
public partial class UnitSettings
{
    
    [Inject] public required IConfigurationService ConfigService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    
    private UnitConfiguration? _config;

    private bool _isLoading = true;
    private bool _isSaving;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private void ClearError() => _errorMessage = string.Empty;
    private void ClearSuccess() => _successMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadConfiguration();
    }

    private async Task LoadConfiguration()
    {
        _isLoading = true;
        try
        {
            _config = await ConfigService.GetConfigurationAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading configuration: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveSettings()
    {
        if (_config == null) return;

        _isSaving = true;
        _errorMessage = string.Empty;
        _successMessage = string.Empty;

        try
        {
            await ConfigService.UpdateConfigurationAsync(_config);
            _successMessage = "Settings saved successfully!";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error saving settings: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

}