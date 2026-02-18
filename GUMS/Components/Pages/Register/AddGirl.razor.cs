using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Register;

public partial class AddGirl
{
    [Inject] public required IPersonService PersonService { get; set; }
    [Inject] public required IConfigurationService ConfigService { get; set; }
    [Inject] public required IPaymentService PaymentService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    private Person _person = new()
    {
        PersonType = PersonType.Girl,
        IsActive = true,
        EmergencyContacts = []
    };

    private DateTime _dateJoined = DateTime.Today;
    private bool _waiveJoiningFee;
    private decimal _joiningFeeAmount;
    private int _paymentTermDays;
    private string? _errorMessage;
    private bool _isProcessing;

    protected override async Task OnInitializedAsync()
    {
        var config = await ConfigService.GetConfigurationAsync();
        _person.Section = config.UnitType;
        _joiningFeeAmount = config.JoiningFeeAmount;
        _paymentTermDays = config.PaymentTermDays;

        _person.EmergencyContacts.Add(new EmergencyContact
        {
            ContactName = string.Empty,
            Relationship = string.Empty,
            PrimaryPhone = string.Empty,
            SortOrder = 0
        });
    }

    private async Task HandleSubmit()
    {
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

            _person.DateJoined = _dateJoined;

            await PersonService.AddAsync(_person);

            if (_joiningFeeAmount > 0 && !_waiveJoiningFee)
            {
                var payment = new Payment
                {
                    MembershipNumber = _person.MembershipNumber,
                    Amount = _joiningFeeAmount,
                    PaymentType = PaymentType.Other,
                    DueDate = DateTime.Today.AddDays(_paymentTermDays),
                    Reference = "Joining Fee",
                    Status = PaymentStatus.Pending
                };
                await PaymentService.CreateAsync(payment);
            }

            NavigationManager.NavigateTo("/Register");
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error saving girl: {ex.Message}";
        }
        finally
        {
            _isProcessing = false;
        }
    }
}
