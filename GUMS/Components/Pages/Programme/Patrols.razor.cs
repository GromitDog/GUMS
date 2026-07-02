using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Programme;

public partial class Patrols
{
    [Inject] public required IPatrolService PatrolService { get; set; }
    [Inject] public required IProgrammeService ProgrammeService { get; set; }
    [Inject] public required IConfigurationService ConfigurationService { get; set; }

    private Section _activeSection = Section.Brownie;
    private bool _sectionSupported = true;
    private List<Patrol> _patrols = new();
    private List<Person> _unassigned = new();
    private Dictionary<string, HashSet<int>> _awardedBadges = new();
    private Dictionary<PatrolRole, int> _roleBadgeIds = new();

    private bool _isLoading = true;
    private bool _isSaving;
    private string? _errorMessage;

    private bool _showCreateModal;
    private string _newPatrolName = string.Empty;

    private int? _renamingPatrolId;
    private string _renamePatrolName = string.Empty;

    private int? _confirmDeletePatrolId;

    protected override async Task OnInitializedAsync()
    {
        var config = await ConfigurationService.GetConfigurationAsync();
        _activeSection = config.UnitType;
        _sectionSupported = _activeSection == Section.Brownie || _activeSection == Section.Guide;

        if (_sectionSupported)
            await LoadAsync();
        else
            _isLoading = false;
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            _patrols = await PatrolService.GetPatrolsAsync(_activeSection);
            _unassigned = await PatrolService.GetUnassignedGirlsAsync(_activeSection);
            _awardedBadges = await PatrolService.GetAwardedPatrolBadgeMapAsync(_activeSection);

            _roleBadgeIds = new Dictionary<PatrolRole, int>
            {
                [PatrolRole.Leader] = await PatrolService.GetRoleBadgeDefinitionIdAsync(_activeSection, PatrolRole.Leader),
                [PatrolRole.Seconder] = await PatrolService.GetRoleBadgeDefinitionIdAsync(_activeSection, PatrolRole.Seconder)
            };
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OpenCreateModal()
    {
        _newPatrolName = string.Empty;
        _errorMessage = null;
        _showCreateModal = true;
    }

    private void CloseCreateModal()
    {
        _showCreateModal = false;
    }

    private async Task CreatePatrol()
    {
        if (_isSaving) return;
        _isSaving = true;
        try
        {
            var result = await PatrolService.CreatePatrolAsync(_newPatrolName, _activeSection);
            if (!result.Success)
            {
                _errorMessage = result.ErrorMessage;
                return;
            }
            _showCreateModal = false;
            await LoadAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void StartRename(Patrol patrol)
    {
        _renamingPatrolId = patrol.Id;
        _renamePatrolName = patrol.Name;
        _errorMessage = null;
    }

    private void CancelRename()
    {
        _renamingPatrolId = null;
        _renamePatrolName = string.Empty;
    }

    private async Task SaveRename()
    {
        if (_isSaving || !_renamingPatrolId.HasValue) return;
        _isSaving = true;
        try
        {
            var result = await PatrolService.RenamePatrolAsync(_renamingPatrolId.Value, _renamePatrolName);
            if (!result.Success)
            {
                _errorMessage = result.ErrorMessage;
                return;
            }
            _renamingPatrolId = null;
            _renamePatrolName = string.Empty;
            await LoadAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void AskConfirmDelete(int patrolId)
    {
        _confirmDeletePatrolId = patrolId;
        _errorMessage = null;
    }

    private void CancelDelete()
    {
        _confirmDeletePatrolId = null;
    }

    private async Task ConfirmDelete()
    {
        if (_isSaving || !_confirmDeletePatrolId.HasValue) return;
        _isSaving = true;
        try
        {
            var result = await PatrolService.DeletePatrolAsync(_confirmDeletePatrolId.Value);
            if (!result.Success)
            {
                _errorMessage = result.ErrorMessage;
                return;
            }
            _confirmDeletePatrolId = null;
            await LoadAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task AddMember(int patrolId, ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        if (string.IsNullOrEmpty(value) || _isSaving) return;

        _isSaving = true;
        try
        {
            var result = await PatrolService.AssignGirlToPatrolAsync(value, patrolId);
            if (!result.Success)
                _errorMessage = result.ErrorMessage;
            await LoadAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task RemoveMember(string membershipNumber)
    {
        if (_isSaving) return;
        _isSaving = true;
        try
        {
            var result = await PatrolService.RemoveGirlFromPatrolAsync(membershipNumber);
            if (!result.Success)
                _errorMessage = result.ErrorMessage;
            await LoadAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task ChangeRole(string membershipNumber, ChangeEventArgs e)
    {
        if (_isSaving) return;
        if (!Enum.TryParse<PatrolRole>(e.Value?.ToString(), out var role))
            return;

        _isSaving = true;
        try
        {
            var result = await PatrolService.SetRoleAsync(membershipNumber, role);
            if (!result.Success)
                _errorMessage = result.ErrorMessage;
            await LoadAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task ToggleBadge(string membershipNumber, int badgeDefinitionId, bool isAwarded)
    {
        if (_isSaving || badgeDefinitionId == 0) return;
        _isSaving = true;
        try
        {
            if (isAwarded)
                await ProgrammeService.UnmarkBadgeAwardedAsync(membershipNumber, badgeDefinitionId);
            else
                await ProgrammeService.MarkBadgeAwardedAsync(membershipNumber, badgeDefinitionId);
            await LoadAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private bool HasBadge(string membershipNumber, int badgeDefinitionId) =>
        _awardedBadges.TryGetValue(membershipNumber, out var set) && set.Contains(badgeDefinitionId);

    private string GroupSingular => _activeSection == Section.Brownie ? "Six" : "Patrol";
    private string GroupPlural => _activeSection == Section.Brownie ? "Sixes" : "Patrols";
    private string MembersPlural => _activeSection == Section.Brownie ? "Brownies" : "Guides";

    private static string LeaderLabel(Section section) =>
        section == Section.Brownie ? "Sixer" : "Patrol Leader";

    private static string SeconderLabel(Section _) => "Seconder";

    private static string RoleDisplay(Section section, PatrolRole role) => role switch
    {
        PatrolRole.Leader => LeaderLabel(section),
        PatrolRole.Seconder => SeconderLabel(section),
        _ => "Member"
    };

    private IEnumerable<Person> OrderedMembers(Patrol patrol) =>
        patrol.Members.OrderBy(m =>
            m.PatrolRole == PatrolRole.Leader ? 0 :
            m.PatrolRole == PatrolRole.Seconder ? 1 : 2)
        .ThenBy(m => m.FullName);
}
