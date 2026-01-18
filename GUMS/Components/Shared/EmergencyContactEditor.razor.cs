using GUMS.Data.Entities;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Shared;

public partial class EmergencyContactEditor
{
    [Parameter]
    public List<EmergencyContact> Contacts { get; set; } = [];

    [Parameter]
    public EventCallback<List<EmergencyContact>> ContactsChanged { get; set; }

    private void AddContact()
    {
        var newContact = new EmergencyContact
        {
            SortOrder = Contacts.Count > 0 ? Contacts.Max(c => c.SortOrder) + 1 : 0
        };

        Contacts.Add(newContact);
        ContactsChanged.InvokeAsync(Contacts);
    }

    private void RemoveContact(EmergencyContact contact)
    {
        Contacts.Remove(contact);

        for (int i = 0; i < Contacts.Count; i++)
        {
            Contacts[i].SortOrder = i;
        }

        ContactsChanged.InvokeAsync(Contacts);
    }
}
