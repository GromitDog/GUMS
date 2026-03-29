using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GUMS.Components.Pages.Meetings;

[Authorize]
public partial class HomeContact
{
    [Parameter] public int MeetingId { get; set; }

    [Inject] public required IHomeContactService HomeContactService { get; set; }
    [Inject] public required IMeetingService MeetingService { get; set; }
    [Inject] public required IPersonService PersonService { get; set; }
    [Inject] public required IAttendanceService AttendanceService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    private Meeting? _meeting;
    private List<EventHomeContact> _homeContacts = new();
    private List<HomeContactAttendee> _attendees = new();
    private List<EventAdditionalPerson> _additionalPeople = new();

    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isGenerating;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private void ClearError() => _errorMessage = string.Empty;
    private void ClearSuccess() => _successMessage = string.Empty;

    // Add home contact form
    private bool _showAddHC;
    private EventHomeContact _editingHC = new();

    // Edit contact overrides modal
    private bool _showOverrideModal;
    private HomeContactAttendee? _overridePerson;
    private List<EventContactOverride> _editingOverrides = new();

    // Add additional person form
    private bool _showAddPerson;
    private EventAdditionalPerson _editingPerson = new();

    // Delete confirmations
    private bool _showDeleteHC;
    private EventHomeContact? _hcToDelete;
    private bool _showDeletePerson;
    private EventAdditionalPerson? _personToDelete;

    // Generate document
    private bool _showGenerateModal;
    private string _docPassword = string.Empty;
    private string _docPasswordConfirm = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        _meeting = await MeetingService.GetByIdAsync(MeetingId);
        if (_meeting == null)
        {
            NavigationManager.NavigateTo("/Meetings");
            return;
        }

        _homeContacts = await HomeContactService.GetHomeContactsAsync(MeetingId);
        _additionalPeople = await HomeContactService.GetAdditionalPeopleAsync(MeetingId);

        // Get declined membership numbers for this meeting
        var attendance = await AttendanceService.GetAttendanceForMeetingAsync(MeetingId);
        var declinedNumbers = attendance
            .Where(a => a.ConsentDeclined)
            .Select(a => a.MembershipNumber)
            .ToHashSet();

        // Get all active members
        var allMembers = await PersonService.GetActiveAsync();
        var overrides = await HomeContactService.GetContactOverridesAsync(MeetingId);
        var overridesByMember = overrides
            .GroupBy(o => o.MembershipNumber)
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.SortOrder).ToList());

        _attendees = allMembers
            .Where(p => !declinedNumbers.Contains(p.MembershipNumber) && !p.IsDataRemoved)
            .OrderByDescending(p => p.PersonType == PersonType.Leader)
            .ThenBy(p => p.FullName)
            .Select(p =>
            {
                var hasOverrides = overridesByMember.ContainsKey(p.MembershipNumber);
                List<HomeContactEmergencyContact> contacts;

                if (hasOverrides)
                {
                    contacts = overridesByMember[p.MembershipNumber]
                        .Select(o => new HomeContactEmergencyContact
                        {
                            ContactName = o.ContactName,
                            Relationship = o.Relationship,
                            PrimaryPhone = o.PrimaryPhone,
                            SecondaryPhone = o.SecondaryPhone
                        }).ToList();
                }
                else
                {
                    contacts = p.EmergencyContacts
                        .OrderBy(ec => ec.SortOrder)
                        .Select(ec => new HomeContactEmergencyContact
                        {
                            ContactName = ec.ContactName,
                            Relationship = ec.Relationship,
                            PrimaryPhone = ec.PrimaryPhone,
                            SecondaryPhone = ec.SecondaryPhone
                        }).ToList();
                }

                return new HomeContactAttendee
                {
                    MembershipNumber = p.MembershipNumber,
                    FullName = p.FullName ?? p.MembershipNumber,
                    IsLeader = p.PersonType == PersonType.Leader,
                    Phone = p.Phone,
                    EmergencyContacts = contacts,
                    HasOverrides = hasOverrides
                };
            })
            .ToList();

        _isLoading = false;
    }

    // ── Home Contact CRUD ──

    private void ShowAddHomeContact()
    {
        _editingHC = new EventHomeContact { MeetingId = MeetingId };
        _showAddHC = true;
    }

    private void EditHomeContact(EventHomeContact hc)
    {
        _editingHC = new EventHomeContact
        {
            Id = hc.Id,
            MeetingId = hc.MeetingId,
            Name = hc.Name,
            Phone = hc.Phone,
            Notes = hc.Notes,
            SortOrder = hc.SortOrder
        };
        _showAddHC = true;
    }

    private void CancelAddHC() => _showAddHC = false;

    private async Task SaveHomeContact()
    {
        _isSaving = true;
        _errorMessage = string.Empty;
        var (success, error) = await HomeContactService.SaveHomeContactAsync(_editingHC);
        if (success)
        {
            _showAddHC = false;
            _successMessage = _editingHC.Id == 0 ? "Home contact added." : "Home contact updated.";
            _homeContacts = await HomeContactService.GetHomeContactsAsync(MeetingId);
        }
        else
        {
            _errorMessage = error;
        }
        _isSaving = false;
    }

    private void ConfirmDeleteHC(EventHomeContact hc)
    {
        _hcToDelete = hc;
        _showDeleteHC = true;
    }

    private void CancelDeleteHC()
    {
        _hcToDelete = null;
        _showDeleteHC = false;
    }

    private async Task DeleteHomeContact()
    {
        if (_hcToDelete == null) return;
        var (success, error) = await HomeContactService.DeleteHomeContactAsync(_hcToDelete.Id);
        if (success)
        {
            _successMessage = "Home contact removed.";
            _homeContacts = await HomeContactService.GetHomeContactsAsync(MeetingId);
        }
        else _errorMessage = error;
        _showDeleteHC = false;
        _hcToDelete = null;
    }

    // ── Contact Overrides ──

    private async Task ShowOverrides(HomeContactAttendee attendee)
    {
        _overridePerson = attendee;
        var existing = await HomeContactService.GetContactOverridesForMemberAsync(MeetingId, attendee.MembershipNumber);

        if (existing.Count > 0)
        {
            _editingOverrides = existing;
        }
        else
        {
            // Pre-populate from current contacts (defaults)
            _editingOverrides = attendee.EmergencyContacts.Select((c, i) => new EventContactOverride
            {
                MeetingId = MeetingId,
                MembershipNumber = attendee.MembershipNumber,
                ContactName = c.ContactName,
                Relationship = c.Relationship,
                PrimaryPhone = c.PrimaryPhone,
                SecondaryPhone = c.SecondaryPhone,
                SortOrder = i
            }).ToList();
        }

        // Ensure at least 2 slots
        while (_editingOverrides.Count < 2)
        {
            _editingOverrides.Add(new EventContactOverride
            {
                MeetingId = MeetingId,
                MembershipNumber = attendee.MembershipNumber,
                SortOrder = _editingOverrides.Count
            });
        }

        _showOverrideModal = true;
    }

    private void CancelOverrides() => _showOverrideModal = false;

    private async Task SaveOverrides()
    {
        if (_overridePerson == null) return;
        _isSaving = true;
        _errorMessage = string.Empty;

        // Delete existing overrides first
        await HomeContactService.DeleteAllOverridesForMemberAsync(MeetingId, _overridePerson.MembershipNumber);

        // Save non-empty overrides
        foreach (var ov in _editingOverrides.Where(o => !string.IsNullOrWhiteSpace(o.ContactName)))
        {
            ov.Id = 0; // force insert
            var (success, error) = await HomeContactService.SaveContactOverrideAsync(ov);
            if (!success)
            {
                _errorMessage = error;
                _isSaving = false;
                return;
            }
        }

        _showOverrideModal = false;
        _successMessage = $"Emergency contacts updated for {_overridePerson.FullName}.";
        await LoadData();
        _isSaving = false;
    }

    private async Task ResetToDefaults(HomeContactAttendee attendee)
    {
        await HomeContactService.DeleteAllOverridesForMemberAsync(MeetingId, attendee.MembershipNumber);
        _successMessage = $"Contacts reset to defaults for {attendee.FullName}.";
        await LoadData();
    }

    // ── Additional People ──

    private void ShowAddPerson()
    {
        _editingPerson = new EventAdditionalPerson { MeetingId = MeetingId };
        _showAddPerson = true;
    }

    private void EditAdditionalPerson(EventAdditionalPerson person)
    {
        _editingPerson = new EventAdditionalPerson
        {
            Id = person.Id,
            MeetingId = person.MeetingId,
            Name = person.Name,
            Role = person.Role,
            Phone = person.Phone,
            EmergencyContactName = person.EmergencyContactName,
            EmergencyContactPhone = person.EmergencyContactPhone,
            EmergencyContactRelationship = person.EmergencyContactRelationship,
            Notes = person.Notes
        };
        _showAddPerson = true;
    }

    private void CancelAddPerson() => _showAddPerson = false;

    private async Task SaveAdditionalPerson()
    {
        _isSaving = true;
        _errorMessage = string.Empty;
        var (success, error) = await HomeContactService.SaveAdditionalPersonAsync(_editingPerson);
        if (success)
        {
            _showAddPerson = false;
            _successMessage = "Additional person saved.";
            _additionalPeople = await HomeContactService.GetAdditionalPeopleAsync(MeetingId);
        }
        else _errorMessage = error;
        _isSaving = false;
    }

    private void ConfirmDeletePerson(EventAdditionalPerson person)
    {
        _personToDelete = person;
        _showDeletePerson = true;
    }

    private void CancelDeletePerson()
    {
        _personToDelete = null;
        _showDeletePerson = false;
    }

    private async Task DeleteAdditionalPerson()
    {
        if (_personToDelete == null) return;
        var (success, error) = await HomeContactService.DeleteAdditionalPersonAsync(_personToDelete.Id);
        if (success)
        {
            _successMessage = "Additional person removed.";
            _additionalPeople = await HomeContactService.GetAdditionalPeopleAsync(MeetingId);
        }
        else _errorMessage = error;
        _showDeletePerson = false;
        _personToDelete = null;
    }

    // ── Document Generation ──

    private void ShowGenerate()
    {
        _docPassword = string.Empty;
        _docPasswordConfirm = string.Empty;
        _showGenerateModal = true;
    }

    private void CancelGenerate() => _showGenerateModal = false;

    private bool IsPasswordStrong(string pw)
    {
        if (string.IsNullOrWhiteSpace(pw) || pw.Length < 6) return false;
        bool hasUpper = pw.Any(char.IsUpper);
        bool hasLower = pw.Any(char.IsLower);
        bool hasDigit = pw.Any(char.IsDigit);
        return hasUpper && hasLower && hasDigit;
    }

    private async Task GenerateDocument()
    {
        _errorMessage = string.Empty;

        if (!IsPasswordStrong(_docPassword))
        {
            _errorMessage = "Password must be at least 6 characters with uppercase, lowercase, and a digit.";
            return;
        }

        if (_docPassword != _docPasswordConfirm)
        {
            _errorMessage = "Passwords do not match.";
            return;
        }

        if (_homeContacts.Count == 0)
        {
            _errorMessage = "Please add at least one home contact before generating the document.";
            return;
        }

        _isGenerating = true;
        try
        {
            var bytes = await HomeContactService.GenerateHomeContactSheetAsync(MeetingId, _docPassword);
            var fileName = $"HomeContact_{_meeting!.Title.Replace(" ", "_")}_{_meeting.Date:yyyyMMdd}.xlsx";

            // Trigger download via JS interop
            using var streamRef = new DotNetStreamReference(new MemoryStream(bytes));
            await JS.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);

            _showGenerateModal = false;
            _successMessage = "Home contact sheet generated and downloaded.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error generating document: {ex.Message}";
        }
        _isGenerating = false;
    }
}
