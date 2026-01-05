using FluentAssertions;
using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Tests.Services;

public class PersonServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PersonService _sut; // System Under Test

    public PersonServiceTests()
    {
        // Arrange - Create in-memory database for each test
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
        _sut = new PersonService(_context);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    [Fact]
    public async Task AddAsync_ShouldSaveContactsCorrectly_WhenEmergencyContactsProvided()
    {
        // Arrange
        var person = new Person
        {
            MembershipNumber = "12345",
            FullName = "Test Girl",
            DateOfBirth = new DateTime(2015, 1, 1),
            PersonType = PersonType.Girl,
            Section = Section.Brownie,
            EmergencyContacts = new List<EmergencyContact>
            {
                new EmergencyContact
                {
                    ContactName = "Jane Smith",
                    Relationship = "Mother",
                    PrimaryPhone = "07123456789",
                    Email = "jane@example.com",
                    SortOrder = 0
                },
                new EmergencyContact
                {
                    ContactName = "John Smith",
                    Relationship = "Father",
                    PrimaryPhone = "07987654321",
                    SecondaryPhone = "01234567890",
                    SortOrder = 1
                }
            }
        };

        // Act
        var result = await _sut.AddAsync(person);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.EmergencyContacts.Should().HaveCount(2);

        var savedPerson = await _sut.GetByIdAsync(result.Id);
        savedPerson.EmergencyContacts.Should().HaveCount(2);
        savedPerson.EmergencyContacts.Should().Contain(c => c.ContactName == "Jane Smith" && c.Relationship == "Mother");
        savedPerson.EmergencyContacts.Should().Contain(c => c.ContactName == "John Smith" && c.Relationship == "Father");
    }

    [Fact]
    public async Task UpdateAsync_ShouldNotDeleteAndReAddContacts_WhenNoContactsChanged()
    {
        // Arrange - Create a person with emergency contacts
        var person = new Person
        {
            MembershipNumber = "12345",
            FullName = "Test Girl",
            DateOfBirth = new DateTime(2015, 1, 1),
            PersonType = PersonType.Girl,
            Section = Section.Brownie,
            EmergencyContacts = new List<EmergencyContact>
            {
                new EmergencyContact
                {
                    ContactName = "Jane Smith",
                    Relationship = "Mother",
                    PrimaryPhone = "07123456789",
                    SortOrder = 0
                }
            }
        };
        var saved = await _sut.AddAsync(person);
        var originalContactId = saved.EmergencyContacts.First().Id;

        // Act - Update person without changing emergency contacts
        saved.FullName = "Updated Name";
        await _sut.UpdateAsync(saved);

        // Assert - Contact should still have same ID (not deleted and re-added)
        var updated = await _sut.GetByIdAsync(saved.Id);
        updated.FullName.Should().Be("Updated Name");
        updated.EmergencyContacts.Should().HaveCount(1);
        updated.EmergencyContacts.First().Id.Should().Be(originalContactId, "contact should not be deleted and re-added");
        updated.EmergencyContacts.First().ContactName.Should().Be("Jane Smith");
    }

    [Fact]
    public async Task UpdateAsync_ShouldPreserveContactId_WhenContactUpdated()
    {
        // Arrange - Create a person with emergency contact
        var person = new Person
        {
            MembershipNumber = "12345",
            FullName = "Test Girl",
            DateOfBirth = new DateTime(2015, 1, 1),
            PersonType = PersonType.Girl,
            Section = Section.Brownie,
            EmergencyContacts = new List<EmergencyContact>
            {
                new EmergencyContact
                {
                    ContactName = "Jane Smith",
                    Relationship = "Mother",
                    PrimaryPhone = "07123456789",
                    SortOrder = 0
                }
            }
        };
        var saved = await _sut.AddAsync(person);
        var originalContactId = saved.EmergencyContacts.First().Id;

        // Act - Update the emergency contact's phone number
        saved.EmergencyContacts.First().PrimaryPhone = "07999888777";
        await _sut.UpdateAsync(saved);

        // Assert - Contact ID should be preserved
        var updated = await _sut.GetByIdAsync(saved.Id);
        updated.EmergencyContacts.Should().HaveCount(1);
        updated.EmergencyContacts.First().Id.Should().Be(originalContactId, "contact should be updated in place");
        updated.EmergencyContacts.First().PrimaryPhone.Should().Be("07999888777");
    }

    [Fact]
    public async Task UpdateAsync_ShouldAddNewContact_WhenContactAdded()
    {
        // Arrange - Create a person with one emergency contact
        var person = new Person
        {
            MembershipNumber = "12345",
            FullName = "Test Girl",
            DateOfBirth = new DateTime(2015, 1, 1),
            PersonType = PersonType.Girl,
            Section = Section.Brownie,
            EmergencyContacts = new List<EmergencyContact>
            {
                new EmergencyContact
                {
                    ContactName = "Jane Smith",
                    Relationship = "Mother",
                    PrimaryPhone = "07123456789",
                    SortOrder = 0
                }
            }
        };
        var saved = await _sut.AddAsync(person);

        // Act - Add a second emergency contact
        saved.EmergencyContacts.Add(new EmergencyContact
        {
            ContactName = "John Smith",
            Relationship = "Father",
            PrimaryPhone = "07987654321",
            SortOrder = 1
        });
        await _sut.UpdateAsync(saved);

        // Assert - Should have two contacts
        var updated = await _sut.GetByIdAsync(saved.Id);
        updated.EmergencyContacts.Should().HaveCount(2);
        updated.EmergencyContacts.Should().Contain(c => c.ContactName == "Jane Smith");
        updated.EmergencyContacts.Should().Contain(c => c.ContactName == "John Smith");
    }

    [Fact]
    public async Task UpdateAsync_ShouldDeleteContact_WhenContactRemoved()
    {
        // Arrange - Create a person with two emergency contacts
        var person = new Person
        {
            MembershipNumber = "12345",
            FullName = "Test Girl",
            DateOfBirth = new DateTime(2015, 1, 1),
            PersonType = PersonType.Girl,
            Section = Section.Brownie,
            EmergencyContacts = new List<EmergencyContact>
            {
                new EmergencyContact
                {
                    ContactName = "Jane Smith",
                    Relationship = "Mother",
                    PrimaryPhone = "07123456789",
                    SortOrder = 0
                },
                new EmergencyContact
                {
                    ContactName = "John Smith",
                    Relationship = "Father",
                    PrimaryPhone = "07987654321",
                    SortOrder = 1
                }
            }
        };
        var saved = await _sut.AddAsync(person);

        // Act - Remove one contact
        var contactToRemove = saved.EmergencyContacts.First(c => c.ContactName == "John Smith");
        saved.EmergencyContacts.Remove(contactToRemove);
        await _sut.UpdateAsync(saved);

        // Assert - Should have one contact remaining
        var updated = await _sut.GetByIdAsync(saved.Id);
        updated.EmergencyContacts.Should().HaveCount(1);
        updated.EmergencyContacts.First().ContactName.Should().Be("Jane Smith");
    }

    [Fact]
    public async Task UpdateAsync_ShouldHandleAllChangesCorrectly_WhenMixedContactChanges()
    {
        // Arrange - Create a person with three emergency contacts
        var person = new Person
        {
            MembershipNumber = "12345",
            FullName = "Test Girl",
            DateOfBirth = new DateTime(2015, 1, 1),
            PersonType = PersonType.Girl,
            Section = Section.Brownie,
            EmergencyContacts = new List<EmergencyContact>
            {
                new EmergencyContact
                {
                    ContactName = "Jane Smith",
                    Relationship = "Mother",
                    PrimaryPhone = "07123456789",
                    SortOrder = 0
                },
                new EmergencyContact
                {
                    ContactName = "John Smith",
                    Relationship = "Father",
                    PrimaryPhone = "07987654321",
                    SortOrder = 1
                },
                new EmergencyContact
                {
                    ContactName = "Bob Jones",
                    Relationship = "Grandparent",
                    PrimaryPhone = "07111222333",
                    SortOrder = 2
                }
            }
        };
        var saved = await _sut.AddAsync(person);
        var janeId = saved.EmergencyContacts.First(c => c.ContactName == "Jane Smith").Id;

        // Act - Update one, remove one, add one
        // Update Jane's phone
        var jane = saved.EmergencyContacts.First(c => c.ContactName == "Jane Smith");
        jane.PrimaryPhone = "07999888777";

        // Remove Bob
        var bob = saved.EmergencyContacts.First(c => c.ContactName == "Bob Jones");
        saved.EmergencyContacts.Remove(bob);

        // Add Sarah
        saved.EmergencyContacts.Add(new EmergencyContact
        {
            ContactName = "Sarah Brown",
            Relationship = "Aunt",
            PrimaryPhone = "07444555666",
            SortOrder = 3
        });

        await _sut.UpdateAsync(saved);

        // Assert
        var updated = await _sut.GetByIdAsync(saved.Id);
        updated.EmergencyContacts.Should().HaveCount(3);

        // Jane should be updated with same ID
        var updatedJane = updated.EmergencyContacts.First(c => c.ContactName == "Jane Smith");
        updatedJane.Id.Should().Be(janeId, "existing contact should be updated in place");
        updatedJane.PrimaryPhone.Should().Be("07999888777");

        // John should still exist
        updated.EmergencyContacts.Should().Contain(c => c.ContactName == "John Smith");

        // Bob should be gone
        updated.EmergencyContacts.Should().NotContain(c => c.ContactName == "Bob Jones");

        // Sarah should be added
        updated.EmergencyContacts.Should().Contain(c => c.ContactName == "Sarah Brown");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldLoadEmergencyContacts()
    {
        // Arrange
        var person = new Person
        {
            MembershipNumber = "12345",
            FullName = "Test Girl",
            DateOfBirth = new DateTime(2015, 1, 1),
            PersonType = PersonType.Girl,
            Section = Section.Brownie,
            EmergencyContacts = new List<EmergencyContact>
            {
                new EmergencyContact
                {
                    ContactName = "Jane Smith",
                    Relationship = "Mother",
                    PrimaryPhone = "07123456789",
                    SortOrder = 0
                }
            }
        };
        var saved = await _sut.AddAsync(person);

        // Act
        var retrieved = await _sut.GetByIdAsync(saved.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.EmergencyContacts.Should().NotBeNull();
        retrieved.EmergencyContacts.Should().HaveCount(1);
        retrieved.EmergencyContacts.First().ContactName.Should().Be("Jane Smith");
    }
}
