using GUMS.Data.Entities;

namespace GUMS.Services;

public interface IHomeContactService
{
    // Home contact people CRUD
    Task<List<EventHomeContact>> GetHomeContactsAsync(int meetingId);
    Task<(bool Success, string ErrorMessage)> SaveHomeContactAsync(EventHomeContact contact);
    Task<(bool Success, string ErrorMessage)> DeleteHomeContactAsync(int id);

    // Contact override CRUD
    Task<List<EventContactOverride>> GetContactOverridesAsync(int meetingId);
    Task<List<EventContactOverride>> GetContactOverridesForMemberAsync(int meetingId, string membershipNumber);
    Task<(bool Success, string ErrorMessage)> SaveContactOverrideAsync(EventContactOverride contactOverride);
    Task<(bool Success, string ErrorMessage)> DeleteContactOverrideAsync(int id);
    Task<(bool Success, string ErrorMessage)> DeleteAllOverridesForMemberAsync(int meetingId, string membershipNumber);
    Task<bool> HasOverridesForMemberAsync(int meetingId, string membershipNumber);

    // Additional people CRUD
    Task<List<EventAdditionalPerson>> GetAdditionalPeopleAsync(int meetingId);
    Task<(bool Success, string ErrorMessage)> SaveAdditionalPersonAsync(EventAdditionalPerson person);
    Task<(bool Success, string ErrorMessage)> DeleteAdditionalPersonAsync(int id);

    // Document generation
    Task<byte[]> GenerateHomeContactSheetAsync(int meetingId, string password);
}

public class HomeContactAttendee
{
    public string MembershipNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsLeader { get; set; }
    public string? Phone { get; set; }
    public List<HomeContactEmergencyContact> EmergencyContacts { get; set; } = new();
    public bool HasOverrides { get; set; }
    public bool IsExcluded { get; set; }
}

public class HomeContactEmergencyContact
{
    public string ContactName { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string PrimaryPhone { get; set; } = string.Empty;
    public string? SecondaryPhone { get; set; }
}
