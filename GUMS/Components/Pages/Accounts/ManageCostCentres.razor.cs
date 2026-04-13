using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Accounts;

public partial class ManageCostCentres
{
    [Inject] private ICostCentreService CostCentreService { get; set; } = default!;

    private List<CostCentre> _costCentres = new();
    private Dictionary<int, int> _usageCounts = new();
    private bool _showInactive;
    private bool _isLoading = true;

    // Add form
    private string _newName = string.Empty;
    private bool _isAdding;

    // Edit state
    private int? _editingId;
    private string _editingName = string.Empty;

    private string _successMessage = string.Empty;
    private string _errorMessage = string.Empty;

    // Confirm deactivate
    private int? _confirmDeactivateId;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        try
        {
            _costCentres = await CostCentreService.GetAllAsync(activeOnly: !_showInactive);
            _usageCounts.Clear();
            foreach (var cc in _costCentres)
            {
                _usageCounts[cc.Id] = await CostCentreService.GetUsageCountAsync(cc.Id);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ToggleShowInactive()
    {
        _showInactive = !_showInactive;
        await LoadData();
    }

    private async Task AddCostCentre()
    {
        if (string.IsNullOrWhiteSpace(_newName)) return;

        _isAdding = true;
        _errorMessage = string.Empty;

        var result = await CostCentreService.CreateAsync(_newName);
        if (result.Success)
        {
            _newName = string.Empty;
            _successMessage = $"Cost centre '{result.CostCentre!.Name}' created.";
            await LoadData();
        }
        else
        {
            _errorMessage = result.ErrorMessage;
        }

        _isAdding = false;
    }

    private void StartEdit(CostCentre cc)
    {
        _editingId = cc.Id;
        _editingName = cc.Name;
    }

    private void CancelEdit()
    {
        _editingId = null;
        _editingName = string.Empty;
    }

    private async Task SaveEdit()
    {
        if (_editingId == null || string.IsNullOrWhiteSpace(_editingName)) return;

        _errorMessage = string.Empty;
        var result = await CostCentreService.UpdateAsync(_editingId.Value, _editingName);
        if (result.Success)
        {
            _successMessage = "Cost centre renamed.";
            _editingId = null;
            _editingName = string.Empty;
            await LoadData();
        }
        else
        {
            _errorMessage = result.ErrorMessage;
        }
    }

    private async Task DeactivateCostCentre(int id)
    {
        _errorMessage = string.Empty;
        var result = await CostCentreService.DeactivateAsync(id);
        if (result.Success)
        {
            _successMessage = "Cost centre deactivated.";
            _confirmDeactivateId = null;
            await LoadData();
        }
        else
        {
            _errorMessage = result.ErrorMessage;
        }
    }

    private async Task ReactivateCostCentre(int id)
    {
        _errorMessage = string.Empty;
        var result = await CostCentreService.ReactivateAsync(id);
        if (result.Success)
        {
            _successMessage = "Cost centre reactivated.";
            await LoadData();
        }
        else
        {
            _errorMessage = result.ErrorMessage;
        }
    }
}
