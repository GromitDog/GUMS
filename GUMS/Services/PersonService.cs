using System.Text.Json;
using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Services;

public class PersonService : IPersonService
{
    private readonly ApplicationDbContext _context;

    public PersonService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Person> GetByIdAsync(int id)
    {
        var person = await _context.Persons
            .Include(p => p.EmergencyContacts)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person == null)
        {
            throw new KeyNotFoundException($"Person with ID {id} not found.");
        }

        return person;
    }

    public async Task<Person?> GetByMembershipNumberAsync(string membershipNumber)
    {
        return await _context.Persons
            .Include(p => p.EmergencyContacts)
            .FirstOrDefaultAsync(p => p.MembershipNumber == membershipNumber);
    }

    public async Task<List<Person>> GetAllAsync()
    {
        return await _context.Persons
            .Include(p => p.EmergencyContacts)
            .OrderBy(p => p.FullName)
            .ToListAsync();
    }

    public async Task<List<Person>> GetActiveAsync()
    {
        return await _context.Persons
            .Include(p => p.EmergencyContacts)
            .Where(p => p.IsActive && !p.IsDataRemoved)
            .OrderBy(p => p.FullName)
            .ToListAsync();
    }

    public async Task<List<Person>> GetInactiveAsync()
    {
        return await _context.Persons
            .Include(p => p.EmergencyContacts)
            .Where(p => !p.IsActive)
            .OrderBy(p => p.DateLeft)
            .ThenBy(p => p.FullName)
            .ToListAsync();
    }

    public async Task<List<Person>> GetByTypeAsync(PersonType type, bool activeOnly = true)
    {
        var query = _context.Persons
            .Include(p => p.EmergencyContacts)
            .Where(p => p.PersonType == type);

        if (activeOnly)
        {
            query = query.Where(p => p.IsActive && !p.IsDataRemoved);
        }

        return await query.OrderBy(p => p.FullName).ToListAsync();
    }

    public async Task<List<Person>> GetBySectionAsync(Section section, bool activeOnly = true)
    {
        var query = _context.Persons
            .Include(p => p.EmergencyContacts)
            .Where(p => p.Section == section);

        if (activeOnly)
        {
            query = query.Where(p => p.IsActive && !p.IsDataRemoved);
        }

        return await query.OrderBy(p => p.FullName).ToListAsync();
    }

    public async Task<Person> AddAsync(Person person)
    {
        // Validate membership number is unique
        if (!await IsMembershipNumberUniqueAsync(person.MembershipNumber))
        {
            throw new InvalidOperationException($"Membership number {person.MembershipNumber} already exists.");
        }

        // Set defaults
        person.IsActive = true;
        person.IsDataRemoved = false;
        person.DateJoined = person.DateJoined == default ? DateTime.UtcNow : person.DateJoined;

        _context.Persons.Add(person);
        await _context.SaveChangesAsync();

        return person;
    }

    public async Task<Person> UpdateAsync(Person person)
    {
        // First clear all tracked entities to start fresh
        _context.ChangeTracker.Clear();

        // Load existing person from database
        var existing = await _context.Persons
            .Include(p => p.EmergencyContacts)
            .FirstOrDefaultAsync(p => p.Id == person.Id);

        if (existing == null)
        {
            throw new KeyNotFoundException($"Person with ID {person.Id} not found.");
        }

        // Validate membership number is unique (excluding current person)
        if (existing.MembershipNumber != person.MembershipNumber &&
            !await IsMembershipNumberUniqueAsync(person.MembershipNumber, person.Id))
        {
            throw new InvalidOperationException($"Membership number {person.MembershipNumber} already exists.");
        }

        // Update properties
        existing.MembershipNumber = person.MembershipNumber;
        existing.FullName = person.FullName;
        existing.DateOfBirth = person.DateOfBirth;
        existing.PersonType = person.PersonType;
        existing.Section = person.Section;
        existing.DateJoined = person.DateJoined;
        existing.DateLeft = person.DateLeft;
        existing.IsActive = person.IsActive;
        existing.Allergies = person.Allergies;
        existing.Disabilities = person.Disabilities;
        existing.Notes = person.Notes;
        existing.PhotoPermission = person.PhotoPermission;

        // Update emergency contacts - properly handle collection changes
        var incomingContactIds = person.EmergencyContacts.Where(c => c.Id > 0).Select(c => c.Id).ToList();

        // Remove contacts that are no longer present
        var contactsToRemove = existing.EmergencyContacts.Where(c => !incomingContactIds.Contains(c.Id)).ToList();
        foreach (var contact in contactsToRemove)
        {
            _context.EmergencyContacts.Remove(contact);
        }

        // Update existing contacts and add new ones
        foreach (var incomingContact in person.EmergencyContacts)
        {
            if (incomingContact.Id > 0)
            {
                // Update existing contact
                var existingContact = existing.EmergencyContacts.FirstOrDefault(c => c.Id == incomingContact.Id);
                if (existingContact != null)
                {
                    existingContact.ContactName = incomingContact.ContactName;
                    existingContact.Relationship = incomingContact.Relationship;
                    existingContact.PrimaryPhone = incomingContact.PrimaryPhone;
                    existingContact.SecondaryPhone = incomingContact.SecondaryPhone;
                    existingContact.Email = incomingContact.Email;
                    existingContact.Notes = incomingContact.Notes;
                    existingContact.SortOrder = incomingContact.SortOrder;
                }
            }
            else if (incomingContact.Id == 0)
            {
                // Add new contact - create new entity and set navigation property
                var newContact = new EmergencyContact
                {
                    Person = existing,
                    ContactName = incomingContact.ContactName,
                    Relationship = incomingContact.Relationship,
                    PrimaryPhone = incomingContact.PrimaryPhone,
                    SecondaryPhone = incomingContact.SecondaryPhone,
                    Email = incomingContact.Email,
                    Notes = incomingContact.Notes,
                    SortOrder = incomingContact.SortOrder
                };
                existing.EmergencyContacts.Add(newContact);
            }
        }

        await _context.SaveChangesAsync();

        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var person = await GetByIdAsync(id);

        // Soft delete - just mark as inactive
        person.IsActive = false;
        person.DateLeft = DateTime.UtcNow;

        _context.Persons.Update(person);
        await _context.SaveChangesAsync();
    }

    public async Task<List<Person>> SearchAsync(string searchTerm, bool activeOnly = true)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return activeOnly ? await GetActiveAsync() : await GetAllAsync();
        }

        var query = _context.Persons
            .Include(p => p.EmergencyContacts)
            .Where(p =>
                p.MembershipNumber.Contains(searchTerm) ||
                (p.FullName != null && p.FullName.Contains(searchTerm)));

        if (activeOnly)
        {
            query = query.Where(p => p.IsActive && !p.IsDataRemoved);
        }

        return await query.OrderBy(p => p.FullName).ToListAsync();
    }

    public async Task<string> ExportMemberDataAsync(int personId)
    {
        var person = await GetByIdAsync(personId);

        // Create a comprehensive export of all member data
        var exportData = new
        {
            person.MembershipNumber,
            person.FullName,
            person.DateOfBirth,
            PersonType = person.PersonType.ToString(),
            Section = person.Section?.ToString(),
            person.DateJoined,
            person.DateLeft,
            person.Allergies,
            person.Disabilities,
            person.Notes,
            PhotoPermission = person.PhotoPermission.ToString(),
            EmergencyContacts = person.EmergencyContacts.Select(ec => new
            {
                ec.ContactName,
                ec.Relationship,
                ec.PrimaryPhone,
                ec.SecondaryPhone,
                ec.Email,
                ec.Notes
            }).ToList(),
            ExportDate = DateTime.UtcNow
        };

        return JsonSerializer.Serialize(exportData, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public async Task RemoveMemberDataAsync(int personId, string removedBy, bool dataExported = false)
    {
        var person = await GetByIdAsync(personId);

        // Create audit log BEFORE removing data
        var dataRemovalLog = new DataRemovalLog
        {
            MembershipNumber = person.MembershipNumber,
            PersonName = person.FullName ?? "Unknown",
            RemovalDate = DateTime.UtcNow,
            RemovedBy = removedBy,
            DataExported = dataExported,
            Notes = $"Person type: {person.PersonType}, Section: {person.Section}"
        };

        _context.DataRemovalLogs.Add(dataRemovalLog);

        // Anonymize person data
        person.FullName = null;
        person.DateOfBirth = null;
        person.Allergies = null;
        person.Disabilities = null;
        person.Notes = null;
        person.PhotoPermission = PhotoPermission.None;
        person.IsDataRemoved = true;
        person.IsActive = false;

        // If DateLeft is not set, set it now
        if (!person.DateLeft.HasValue)
        {
            person.DateLeft = DateTime.UtcNow;
        }

        // Delete all emergency contacts (cascade delete)
        _context.EmergencyContacts.RemoveRange(person.EmergencyContacts);

        // Update person
        _context.Persons.Update(person);

        await _context.SaveChangesAsync();

        // Note: Attendance and Payment records remain in database,
        // linked by MembershipNumber (not FK), allowing historical records to persist
    }

    public async Task<bool> IsMembershipNumberUniqueAsync(string membershipNumber, int? excludePersonId = null)
    {
        var query = _context.Persons.Where(p => p.MembershipNumber == membershipNumber);

        if (excludePersonId.HasValue)
        {
            query = query.Where(p => p.Id != excludePersonId.Value);
        }

        return !await query.AnyAsync();
    }
}
