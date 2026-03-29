using System.Drawing;
using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace GUMS.Services;

public class HomeContactService : IHomeContactService
{
    private readonly ApplicationDbContext _context;

    public HomeContactService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ── Home Contact People ──

    public async Task<List<EventHomeContact>> GetHomeContactsAsync(int meetingId)
    {
        return await _context.EventHomeContacts
            .AsNoTracking()
            .Where(hc => hc.MeetingId == meetingId)
            .OrderBy(hc => hc.SortOrder)
            .ToListAsync();
    }

    public async Task<(bool Success, string ErrorMessage)> SaveHomeContactAsync(EventHomeContact contact)
    {
        if (string.IsNullOrWhiteSpace(contact.Name))
            return (false, "Name is required.");
        if (string.IsNullOrWhiteSpace(contact.Phone))
            return (false, "Phone number is required.");

        if (contact.Id == 0)
            _context.EventHomeContacts.Add(contact);
        else
            _context.EventHomeContacts.Update(contact);

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteHomeContactAsync(int id)
    {
        var contact = await _context.EventHomeContacts.FindAsync(id);
        if (contact == null) return (false, "Home contact not found.");

        _context.EventHomeContacts.Remove(contact);
        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    // ── Contact Overrides ──

    public async Task<List<EventContactOverride>> GetContactOverridesAsync(int meetingId)
    {
        return await _context.EventContactOverrides
            .AsNoTracking()
            .Where(co => co.MeetingId == meetingId)
            .OrderBy(co => co.MembershipNumber)
            .ThenBy(co => co.SortOrder)
            .ToListAsync();
    }

    public async Task<List<EventContactOverride>> GetContactOverridesForMemberAsync(int meetingId, string membershipNumber)
    {
        return await _context.EventContactOverrides
            .AsNoTracking()
            .Where(co => co.MeetingId == meetingId && co.MembershipNumber == membershipNumber)
            .OrderBy(co => co.SortOrder)
            .ToListAsync();
    }

    public async Task<(bool Success, string ErrorMessage)> SaveContactOverrideAsync(EventContactOverride contactOverride)
    {
        if (string.IsNullOrWhiteSpace(contactOverride.ContactName))
            return (false, "Contact name is required.");
        if (string.IsNullOrWhiteSpace(contactOverride.PrimaryPhone))
            return (false, "Phone number is required.");

        if (contactOverride.Id == 0)
            _context.EventContactOverrides.Add(contactOverride);
        else
            _context.EventContactOverrides.Update(contactOverride);

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteContactOverrideAsync(int id)
    {
        var contact = await _context.EventContactOverrides.FindAsync(id);
        if (contact == null) return (false, "Contact override not found.");

        _context.EventContactOverrides.Remove(contact);
        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteAllOverridesForMemberAsync(int meetingId, string membershipNumber)
    {
        var overrides = await _context.EventContactOverrides
            .Where(co => co.MeetingId == meetingId && co.MembershipNumber == membershipNumber)
            .ToListAsync();

        _context.EventContactOverrides.RemoveRange(overrides);
        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<bool> HasOverridesForMemberAsync(int meetingId, string membershipNumber)
    {
        return await _context.EventContactOverrides
            .AnyAsync(co => co.MeetingId == meetingId && co.MembershipNumber == membershipNumber);
    }

    // ── Additional People ──

    public async Task<List<EventAdditionalPerson>> GetAdditionalPeopleAsync(int meetingId)
    {
        return await _context.EventAdditionalPeople
            .AsNoTracking()
            .Where(ap => ap.MeetingId == meetingId)
            .OrderBy(ap => ap.Name)
            .ToListAsync();
    }

    public async Task<(bool Success, string ErrorMessage)> SaveAdditionalPersonAsync(EventAdditionalPerson person)
    {
        if (string.IsNullOrWhiteSpace(person.Name))
            return (false, "Name is required.");

        if (person.Id == 0)
            _context.EventAdditionalPeople.Add(person);
        else
            _context.EventAdditionalPeople.Update(person);

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteAdditionalPersonAsync(int id)
    {
        var person = await _context.EventAdditionalPeople.FindAsync(id);
        if (person == null) return (false, "Additional person not found.");

        _context.EventAdditionalPeople.Remove(person);
        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    // ── Document Generation ──

    public async Task<byte[]> GenerateHomeContactSheetAsync(int meetingId, string password)
    {
        var meeting = await _context.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId)
            ?? throw new InvalidOperationException("Meeting not found.");

        var config = await _context.UnitConfigurations.AsNoTracking().FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Unit configuration not found.");

        var homeContacts = await GetHomeContactsAsync(meetingId);

        // Get all active members who haven't declined consent for this event
        var declinedMembershipNumbers = await _context.Attendances
            .Where(a => a.MeetingId == meetingId && a.ConsentDeclined)
            .Select(a => a.MembershipNumber)
            .ToListAsync();

        var activeMembers = await _context.Persons
            .AsNoTracking()
            .Include(p => p.EmergencyContacts.OrderBy(ec => ec.SortOrder))
            .Where(p => p.IsActive && !p.IsDataRemoved)
            .OrderBy(p => p.FullName)
            .ToListAsync();

        // Filter out declined
        activeMembers = activeMembers
            .Where(p => !declinedMembershipNumbers.Contains(p.MembershipNumber))
            .ToList();

        // Sort: leaders first, then girls
        var leaders = activeMembers.Where(p => p.PersonType == PersonType.Leader).ToList();
        var girls = activeMembers.Where(p => p.PersonType == PersonType.Girl).ToList();

        // Get all overrides for this meeting
        var allOverrides = await GetContactOverridesAsync(meetingId);
        var overridesByMember = allOverrides
            .GroupBy(o => o.MembershipNumber)
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.SortOrder).ToList());

        // Get additional people
        var additionalPeople = await GetAdditionalPeopleAsync(meetingId);

        // EPPlus license for non-commercial use
        ExcelPackage.License.SetNonCommercialOrganization("Girlguiding Unit");

        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Home Contact");

        // Page setup
        ws.PrinterSettings.Orientation = eOrientation.Landscape;
        ws.PrinterSettings.FitToPage = true;
        ws.PrinterSettings.FitToWidth = 1;
        ws.PrinterSettings.FitToHeight = 0;
        ws.PrinterSettings.LeftMargin = 0.4;
        ws.PrinterSettings.RightMargin = 0.4;
        ws.PrinterSettings.TopMargin = 0.4;
        ws.PrinterSettings.BottomMargin = 0.4;

        // Colours
        var brandBlue = ColorTranslator.FromHtml("#007BC4");
        var amberBg = ColorTranslator.FromHtml("#FFF3CD");
        var lightGray = ColorTranslator.FromHtml("#F8F9FA");
        var headerBg = ColorTranslator.FromHtml("#E9ECEF");
        var borderColor = ColorTranslator.FromHtml("#DEE2E6");
        var dangerRed = ColorTranslator.FromHtml("#DC3545");
        var amberHeader = ColorTranslator.FromHtml("#D4A017");

        int row = 1;

        // ── Title Section ──
        var dateStr = meeting.EndDate.HasValue
            ? $"{meeting.Date:dddd d MMMM yyyy} - {meeting.EndDate.Value:dddd d MMMM yyyy}"
            : $"{meeting.Date:dddd d MMMM yyyy}";

        SetCell(ws, row, 1, config.UnitName, bold: true, fontSize: 16);
        ws.Cells[row, 1, row, 7].Merge = true;
        row++;

        SetCell(ws, row, 1, meeting.Title, bold: true, fontSize: 14);
        ws.Cells[row, 1, row, 7].Merge = true;
        row++;

        SetCell(ws, row, 1, $"{dateStr}  |  {meeting.LocationName}", fontSize: 11);
        ws.Cells[row, 1, row, 7].Merge = true;
        row += 2;

        // ── Leaders at Event ──
        row = AddSectionHeader(ws, row, "LEADERS AT EVENT", brandBlue);
        SetCell(ws, row, 1, "Name", bold: true, fontSize: 9, bgColor: headerBg);
        SetCell(ws, row, 2, "Phone", bold: true, fontSize: 9, bgColor: headerBg);
        AddBottomBorder(ws, row, 1, 2);
        row++;

        foreach (var leader in leaders)
        {
            SetCell(ws, row, 1, leader.FullName ?? leader.MembershipNumber);
            SetCell(ws, row, 2, leader.Phone ?? "");
            AddHairBorder(ws, row, 1, 2, borderColor);
            row++;
        }
        row++;

        // ── Home Contacts (highlighted) ──
        row = AddSectionHeader(ws, row, "HOME CONTACT(S)", amberHeader);
        SetCell(ws, row, 1, "Name", bold: true, fontSize: 9, bgColor: headerBg);
        SetCell(ws, row, 2, "Phone", bold: true, fontSize: 9, bgColor: headerBg);
        SetCell(ws, row, 3, "Notes", bold: true, fontSize: 9, bgColor: headerBg);
        AddBottomBorder(ws, row, 1, 3);
        row++;

        if (homeContacts.Count == 0)
        {
            SetCell(ws, row, 1, "No home contacts assigned", italic: true);
            ws.Cells[row, 1, row, 3].Merge = true;
            row++;
        }
        else
        {
            foreach (var hc in homeContacts)
            {
                SetCell(ws, row, 1, hc.Name, bold: true, bgColor: amberBg);
                SetCell(ws, row, 2, hc.Phone, bold: true, bgColor: amberBg);
                SetCell(ws, row, 3, hc.Notes ?? "", bgColor: amberBg);
                AddHairBorder(ws, row, 1, 3, borderColor);
                row++;
            }
        }
        row++;

        // ── All Attendees with Emergency Contacts ──
        row = AddSectionHeader(ws, row, "ATTENDEES & EMERGENCY CONTACTS", brandBlue);
        string[] headers = ["Name", "Contact 1 Name", "Relationship", "Phone", "Contact 2 Name", "Relationship", "Phone"];
        for (int c = 0; c < headers.Length; c++)
            SetCell(ws, row, c + 1, headers[c], bold: true, fontSize: 9, bgColor: headerBg);
        AddBottomBorder(ws, row, 1, 7);
        row++;

        var allAttendees = leaders.Concat(girls).ToList();
        bool alt = false;
        foreach (var person in allAttendees)
        {
            var rowBg = alt ? lightGray : (Color?)null;
            SetCell(ws, row, 1, person.FullName ?? person.MembershipNumber,
                bold: person.PersonType == PersonType.Leader, bgColor: rowBg);

            // Get emergency contacts: overrides if present, otherwise defaults
            List<HomeContactEmergencyContact> contacts;
            if (overridesByMember.TryGetValue(person.MembershipNumber, out var overrides))
            {
                contacts = overrides.Select(o => new HomeContactEmergencyContact
                {
                    ContactName = o.ContactName,
                    Relationship = o.Relationship,
                    PrimaryPhone = o.PrimaryPhone,
                    SecondaryPhone = o.SecondaryPhone
                }).ToList();
            }
            else
            {
                contacts = person.EmergencyContacts
                    .OrderBy(ec => ec.SortOrder)
                    .Select(ec => new HomeContactEmergencyContact
                    {
                        ContactName = ec.ContactName,
                        Relationship = ec.Relationship,
                        PrimaryPhone = ec.PrimaryPhone,
                        SecondaryPhone = ec.SecondaryPhone
                    }).ToList();
            }

            // Contact 1
            if (contacts.Count > 0)
            {
                SetCell(ws, row, 2, contacts[0].ContactName, bgColor: rowBg);
                SetCell(ws, row, 3, contacts[0].Relationship, bgColor: rowBg);
                var phone1 = contacts[0].PrimaryPhone
                    + (string.IsNullOrEmpty(contacts[0].SecondaryPhone) ? "" : $" / {contacts[0].SecondaryPhone}");
                SetCell(ws, row, 4, phone1, bgColor: rowBg);
            }

            // Contact 2
            if (contacts.Count > 1)
            {
                SetCell(ws, row, 5, contacts[1].ContactName, bgColor: rowBg);
                SetCell(ws, row, 6, contacts[1].Relationship, bgColor: rowBg);
                var phone2 = contacts[1].PrimaryPhone
                    + (string.IsNullOrEmpty(contacts[1].SecondaryPhone) ? "" : $" / {contacts[1].SecondaryPhone}");
                SetCell(ws, row, 7, phone2, bgColor: rowBg);
            }

            // Fill background on empty cells if alternating
            if (rowBg.HasValue)
            {
                for (int c = 1; c <= 7; c++)
                {
                    if (ws.Cells[row, c].Value == null)
                        ws.Cells[row, c].Style.Fill.SetBackground(rowBg.Value);
                }
            }

            AddHairBorder(ws, row, 1, 7, borderColor);
            alt = !alt;
            row++;
        }
        row++;

        // ── Additional People ──
        if (additionalPeople.Count > 0)
        {
            row = AddSectionHeader(ws, row, "ADDITIONAL PEOPLE", brandBlue);
            string[] apHeaders = ["Name", "Role", "Phone", "Emergency Contact", "Relationship", "EC Phone"];
            for (int c = 0; c < apHeaders.Length; c++)
                SetCell(ws, row, c + 1, apHeaders[c], bold: true, fontSize: 9, bgColor: headerBg);
            AddBottomBorder(ws, row, 1, 6);
            row++;

            foreach (var ap in additionalPeople)
            {
                SetCell(ws, row, 1, ap.Name);
                SetCell(ws, row, 2, ap.Role ?? "");
                SetCell(ws, row, 3, ap.Phone ?? "");
                SetCell(ws, row, 4, ap.EmergencyContactName ?? "");
                SetCell(ws, row, 5, ap.EmergencyContactRelationship ?? "");
                SetCell(ws, row, 6, ap.EmergencyContactPhone ?? "");
                AddHairBorder(ws, row, 1, 6, borderColor);
                row++;
            }
            row++;
        }

        // ── Emergency Numbers ──
        row = AddSectionHeader(ws, row, "EMERGENCY NUMBERS", dangerRed);

        if (!string.IsNullOrWhiteSpace(config.DistrictCommissionerName))
        {
            SetCell(ws, row, 1, "District Commissioner", bold: true);
            SetCell(ws, row, 2, config.DistrictCommissionerName);
            SetCell(ws, row, 3, config.DistrictCommissionerPhone ?? "");
            AddHairBorder(ws, row, 1, 3, borderColor);
            row++;
        }

        if (!string.IsNullOrWhiteSpace(config.DivisionCommissionerName))
        {
            SetCell(ws, row, 1, "Division Commissioner", bold: true);
            SetCell(ws, row, 2, config.DivisionCommissionerName);
            SetCell(ws, row, 3, config.DivisionCommissionerPhone ?? "");
            AddHairBorder(ws, row, 1, 3, borderColor);
            row++;
        }

        SetCell(ws, row, 1, "Girlguiding Emergency Safety Line", bold: true);
        SetCell(ws, row, 2, "0207 592 1828", bold: true);
        AddHairBorder(ws, row, 1, 3, borderColor);
        row++;

        SetCell(ws, row, 1, "In the event of a serious accident or death, Girlguiding must be contacted within 1 hour via the emergency safety line.",
            italic: true, fontSize: 9);
        ws.Cells[row, 1, row, 7].Merge = true;
        row++;

        SetCell(ws, row, 1, "Emergency Services: 999  |  Non-Emergency Police: 101  |  NHS: 111", bold: true);
        ws.Cells[row, 1, row, 7].Merge = true;

        // ── Column widths ──
        ws.Column(1).Width = 26;
        ws.Column(2).Width = 22;
        ws.Column(3).Width = 15;
        ws.Column(4).Width = 24;
        ws.Column(5).Width = 22;
        ws.Column(6).Width = 15;
        ws.Column(7).Width = 24;

        // Set print area
        ws.PrinterSettings.PrintArea = ws.Cells[1, 1, row, 7];

        // Save with AES-256 file-level encryption — requires password to open
        using var stream = new MemoryStream();
        package.SaveAs(stream, password);
        return stream.ToArray();
    }

    // ── Helper methods ──

    private static void SetCell(ExcelWorksheet ws, int row, int col, string value,
        bool bold = false, bool italic = false, int fontSize = 10,
        Color? bgColor = null, Color? fontColor = null)
    {
        var cell = ws.Cells[row, col];
        cell.Value = value;
        cell.Style.Font.Name = "Arial";
        cell.Style.Font.Size = fontSize;
        cell.Style.Font.Bold = bold;
        cell.Style.Font.Italic = italic;
        if (bgColor.HasValue)
            cell.Style.Fill.SetBackground(bgColor.Value);
        if (fontColor.HasValue)
            cell.Style.Font.Color.SetColor(fontColor.Value);
    }

    private static int AddSectionHeader(ExcelWorksheet ws, int row, string title, Color bgColor)
    {
        ws.Cells[row, 1, row, 7].Merge = true;
        var cell = ws.Cells[row, 1];
        cell.Value = title;
        cell.Style.Font.Name = "Arial";
        cell.Style.Font.Size = 11;
        cell.Style.Font.Bold = true;
        cell.Style.Font.Color.SetColor(Color.White);
        ws.Cells[row, 1, row, 7].Style.Fill.SetBackground(bgColor);
        return row + 1;
    }

    private static void AddBottomBorder(ExcelWorksheet ws, int row, int colStart, int colEnd)
    {
        ws.Cells[row, colStart, row, colEnd].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
    }

    private static void AddHairBorder(ExcelWorksheet ws, int row, int colStart, int colEnd, Color color)
    {
        var range = ws.Cells[row, colStart, row, colEnd];
        range.Style.Border.Bottom.Style = ExcelBorderStyle.Hair;
        range.Style.Border.Bottom.Color.SetColor(color);
    }
}
