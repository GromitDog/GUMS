using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Register;

public partial class EditMember
{
    [Inject] public required IPersonService PersonService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    [Parameter]
    public int Id { get; set; }

    private Person? _person;
    private string? _errorMessage;
    private bool _isLoading = true;
    private bool _isProcessing;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _person = await PersonService.GetByIdAsync(Id);
        }
        catch
        {
            _person = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task HandleSubmit()
    {
        if (_person == null) return;

        _isProcessing = true;
        _errorMessage = null;

        try
        {
            if (!_person.EmergencyContacts.Any())
            {
                _errorMessage = "At least one emergency contact is required.";
                return;
            }

            foreach (var contact in _person.EmergencyContacts)
            {
                if (string.IsNullOrWhiteSpace(contact.ContactName) ||
                    string.IsNullOrWhiteSpace(contact.Relationship) ||
                    string.IsNullOrWhiteSpace(contact.PrimaryPhone))
                {
                    _errorMessage = "All emergency contacts must have a name, relationship, and primary phone number.";
                    return;
                }
            }

            await PersonService.UpdateAsync(_person);

            NavigationManager.NavigateTo($"/Register/View/{Id}");
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error saving changes: {ex.Message}";
        }
        finally
        {
            _isProcessing = false;
        }
    }
}
