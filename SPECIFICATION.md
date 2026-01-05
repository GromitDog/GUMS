# Girl Guide Unit Management System (GUMS) - Specification

## Overview
A management system for Girl Guide units to handle member registration, meeting planning, attendance tracking, and payment management. Initially designed for single-unit use with potential for multi-unit expansion.

## Core Principles
- **Security First**: Sensitive personal data must be encrypted and password-protected
- **Data Protection**: Support right to be forgotten while maintaining necessary records
- **Simplicity**: Focus on essential features that solve real problems
- **Girl Guiding Compliance**: Follow [Girl Guiding brand guidelines](https://girlguiding.foleon.com/girlguiding-brand-guidelines/brand-guidelines/)
- **Future-Proof**: Design with potential expansion in mind without over-engineering

## Technology Stack
- **Backend**: ASP.NET Core with Entity Framework Core
- **Frontend**: Blazor Server (current project setup)
- **Database**: SQL Server or SQLite with encryption
- **Authentication**: Simple password-based authentication (single user initially)

---

## Feature Specifications

### 1. Member Register

#### 1.1 Person Types
- **Girls**: Unit members who attend meetings
- **Leaders**: Adult volunteers who run the unit

#### 1.2 Member Information

**Core Details:**
- Membership Number (required, unique, **retained after member leaves**)
- Full Name (required)
- Date of Birth (required)
- Section (Rainbows/Brownies/Guides/Rangers) - for girls only

**Emergency Contacts** (Multiple contacts supported for split families):
- Contact Name (required)
- Relationship (required)
- Primary Phone (required)
- Secondary Phone (optional)
- Email (optional)
- Notes (e.g., "Lives with Mon-Wed")

**Medical & Wellbeing:**
- Allergies/Intolerances
- Disabilities/Additional Needs
- General Notes

**Permissions:**
- Photo Permission Levels:
  - No photos
  - Unit use only (internal communications, displays)
  - All media (including social media, publicity)

**Metadata:**
- Date Joined
- Date Left (optional - for members who leave)
- Active Status (active/inactive)

#### 1.3 Member Management Features
- Add new members
- Edit member details
- View member list (filterable by type, section, active status)
- Search members by name or membership number
- Mark members as leaving (triggers data removal process)
- Generate email contact lists (BCC-ready format, includes all contacts)

#### 1.4 Data Removal Process
When a member leaves:
1. Mark as inactive with Date Left
2. Remove all personal data (name, DoB, contacts, medical info, permissions, etc.)
3. **Retain only**: Membership Number
4. **Keep**: All attendance records (linked to membership number)
5. **Keep**: All payment records (linked to membership number)
6. This allows historical records and financial audit trail while respecting data protection

---

### 2. Meetings Management

#### 2.1 Meeting Types

**Regular Meetings:**
- Weekly meetings on the same day of the week
- Follow school term patterns
- Automatically suggested based on term dates

**Extra Meetings:**
- Special events, trips, camps
- May have associated costs
- May require consent forms

#### 2.2 Meeting Information

**Basic Details:**
- Date (required)
- Start Time (required)
- End Time (required)
- Meeting Type (Regular/Extra)
- Title/Description

**Location:**
- Location Name
- Address
- Defaults to configured "home location" for regular meetings

**Activities:**
- List of planned activities
- Flag if activity requires consent form
- Notes for each activity

**Costs:**
- Cost per attendee (for extra meetings)
- Supports partial payments for larger amounts (e.g., camps)
- Payment deadline

#### 2.3 Attendance Tracking
- Mark which members attended each meeting
- Quick entry interface for regular meetings (checklist of active members)
- For extra meetings, track sign-ups vs actual attendance
- Flag for follow-up: full term of absences without prior discussion

#### 2.4 Consent Forms
For activities requiring consent:
- Track consent status per member:
  - Not received
  - Email confirmation received (parent emailed but form not yet returned)
  - Physical form received
- Date of email confirmation
- Date of physical form received
- Flag for follow-up on outstanding physical forms
- Link to payment due if activity has cost

---

### 3. Payments Management

#### 3.1 Payment Types

**Termly Subscriptions:**
- Automatically generated at start of term for all active girls
- Standard amount per term (configurable)
- Can be split into partial payments if needed

**Activity Payments:**
- Generated when a girl signs up for a costed extra meeting/activity
- Amount specific to that activity
- Supports partial payments (especially for expensive camps)

#### 3.2 Payment Records

**Payment Due:**
- Member (by Membership Number - persists even if member leaves)
- Amount
- Type (Subs/Activity)
- Due Date
- Term/Activity Reference
- Status (Pending/Paid/Cancelled)
- Payment Date (when marked as paid)
- Amount Paid (supports partial payments)
- Outstanding Balance
- Notes

#### 3.3 Payment Management
- View all payments due (filterable by status, member, term)
- Mark payments as paid (with date and amount)
- Support partial payments with running balance
- Cancel payments if needed (with reason)
- Generate payment reminder lists
- View payment history by member
- Financial records retained even after member leaves (linked to membership number)

---

### 4. Communications

#### 4.1 Email Contact Lists
- Generate email lists for:
  - All active members
  - Girls only / Leaders only
  - Specific section
  - Specific meeting attendees/sign-ups
  - Parents with outstanding consents
  - Parents with outstanding payments

**Format:**
- BCC-ready (semicolon or comma-separated)
- Includes all emergency contacts with emails
- For girls with multiple contacts (split parents), all emails included
- Displays contact names with emails for reference
- Copy-to-clipboard functionality

**Note:** System generates lists only; actual email sending done externally

---

### 5. Configuration

#### 5.1 Unit Settings
- Unit Name
- Unit Type (Rainbows/Brownies/Guides/Rangers)
- Meeting Day (day of week for regular meetings)
- Default Meeting Times
- Default Location (name and address)

#### 5.2 Term Dates
- Define school terms with start and end dates
- Used for generating regular meetings and subs

#### 5.3 Financial Settings
- Default termly subscription amount
- Payment terms (how many days to pay)

---

### 6. Security & Data Protection

#### 6.1 Authentication
- Password-protected application access
- Single user account initially
- Prepare for role-based access (Admin/Leader/Helper) for future expansion

#### 6.2 Data Encryption
- Database-level encryption (encrypted database file)
- Secure password storage (hashed)
- All sensitive personal data protected at rest

#### 6.3 Data Retention & Right to be Forgotten
- When member leaves: remove all personal data except membership number
- Maintain attendance and payment history linked to membership number
- Support data export for leaving members (before removal)
- Audit trail of data removals
- Prepare for GDPR compliance

#### 6.4 Attendance Monitoring
- No minimum attendance requirement enforced
- System flags full term of absences for leader review
- Leader can add notes to justify extended absences (illness, family situation, etc.)
- Unexplained full-term absences = potential place forfeiture (leader decision)

---

### 7. User Interface

#### 7.1 Navigation Structure
- Dashboard (overview stats, alerts)
- Register (member list and management)
- Meetings (calendar view, upcoming meetings, past meetings)
- Payments (due, paid, overdue)
- Communications (generate email lists)
- Settings/Configuration

#### 7.2 Branding
- Follow [Girl Guiding brand guidelines](https://girlguiding.foleon.com/girlguiding-brand-guidelines/brand-guidelines/)
- Use official colors, fonts, and styling
- Include appropriate logos and imagery

#### 7.3 Key User Journeys

**Adding a new girl:**
1. Navigate to Register
2. Click "Add Girl"
3. Enter all required details including multiple emergency contacts
4. Save - she appears in the register

**Planning a weekly meeting:**
1. Navigate to Meetings
2. View suggested dates based on term calendar
3. Select a date, add activities
4. Save meeting
5. On meeting day, mark attendance

**Adding an extra meeting with cost (e.g., camp):**
1. Navigate to Meetings
2. Click "Add Extra Meeting"
3. Enter details, activities, cost
4. Mark which activities need consent
5. Save meeting
6. Parents email to say yes - mark "email confirmation received"
7. When physical consent form arrives, mark "form received"
8. Payment due is auto-created
9. Receive partial payment, mark amount paid
10. Receive final payment, mark as fully paid

**Managing termly subs:**
1. At start of term, system generates payment due for all active girls
2. Navigate to Payments
3. View list of outstanding payments
4. As payments received, mark as paid with date
5. Follow up on overdue payments using generated email list

**When a member leaves:**
1. Navigate to Register, select member
2. Click "Mark as Left", enter leaving date
3. System offers to export member data
4. Confirm data removal
5. Personal data removed, membership number and records retained

**Generating email list for camp sign-ups:**
1. Navigate to Communications
2. Select "Meeting Attendees" filter
3. Choose the camp meeting
4. System shows all contacts with emails (including both parents for split families)
5. Copy BCC-ready email list
6. Paste into email client

---

## Data Model Overview

### Core Entities

**Person**
- Id, MembershipNumber, Name, DateOfBirth, Type (Girl/Leader)
- Section (for girls), DateJoined, DateLeft, IsActive
- Allergies, Disabilities, Notes
- PhotoPermission (enum: None, UnitOnly, AllMedia)
- IsDataRemoved (flag for left members)

**EmergencyContact**
- Id, PersonId, ContactName, Relationship
- PrimaryPhone, SecondaryPhone, Email
- Notes (for split family arrangements)
- SortOrder (to handle multiple contacts)

**Meeting**
- Id, Date, StartTime, EndTime, MeetingType (Regular/Extra)
- Title, Description, LocationName, LocationAddress
- CostPerAttendee

**Activity**
- Id, MeetingId, Name, Description, RequiresConsent

**Attendance**
- Id, MeetingId, MembershipNumber (not PersonId - persists after data removal)
- Attended (bool)
- ConsentEmailReceived (bool), ConsentEmailDate
- ConsentFormReceived (bool), ConsentFormDate
- Notes

**Payment**
- Id, MembershipNumber (not PersonId - persists after data removal)
- Amount, Type (Subs/Activity)
- DueDate, Status (Pending/Paid/Cancelled)
- AmountPaid, OutstandingBalance
- PaymentDate, Reference (Term/Meeting), Notes

**Term**
- Id, Name, StartDate, EndDate, SubsAmount

**UnitConfiguration**
- Id, UnitName, UnitType, MeetingDayOfWeek
- DefaultMeetingStartTime, DefaultMeetingEndTime
- DefaultLocationName, DefaultLocationAddress
- DefaultSubsAmount, PaymentTermDays

**DataRemovalLog**
- Id, MembershipNumber, RemovalDate, RemovedBy
- DataExported (bool), Notes

---

## Future Enhancements (Out of Scope for V1)

### Phase 2 - Advanced Features
- Badge tracking and progress
- Leader expense management
- Event budgeting and financial reporting
- Multi-user access with roles
- Document storage (consent forms, policies)
- Integrated email sending (currently external)

### Phase 3 - Multi-Unit Support
- Multiple units in single installation
- Unit-specific branding
- Cross-unit reporting
- Shared resource management

### Phase 4 - Advanced Reporting
- Attendance reports and analytics
- Financial reports
- Member demographics
- Export capabilities (Excel, PDF)

---

## Success Criteria

**V1 is successful when:**
1. All active girls and leaders are registered with complete information
2. Weekly meetings can be planned in under 2 minutes
3. Attendance can be recorded during/after meetings easily
4. Termly subs are automatically tracked with minimal effort
5. Extra meetings with costs can be managed end-to-end including partial payments
6. Consent tracking (email + form) prevents forgotten forms
7. Email contact lists can be generated in seconds for any group
8. All sensitive data is encrypted and secure
9. Members leaving have data properly removed while keeping necessary records
10. The interface follows Girl Guiding brand guidelines
11. The system saves at least 2 hours per week vs current method

---

## Technical Considerations

### Database Schema Design
- Use EF Core migrations for schema management
- Design for single-unit but structure supports multi-unit (UnitId in relevant tables)
- Soft deletes for member records (IsActive flag)
- **Critical**: Use MembershipNumber (not PersonId) for Attendance and Payment foreign keys
- Audit trails on sensitive data changes and removals

### Encryption Strategy
- Use SQL Server Transparent Data Encryption (TDE) or SQLite encryption
- Application-level master password to unlock database
- Consider field-level encryption for most sensitive data (emergency contacts, medical info)

### Performance
- Expected dataset: 20-50 members, 40-100 meetings/year, 500-2000 payment records/year
- Performance should not be a concern at this scale
- Standard EF Core practices sufficient

### Backup & Recovery
- Implement regular automated backups
- Export functionality for data portability
- Clear restore process documented

---

## Requirements Summary

### Functional Requirements Answered

1. **Consent Forms**: Parents may email confirmation separately from returning physical form. System tracks both email confirmation and physical form receipt for follow-up purposes.

2. **Partial Payments**: Fully supported. Important for larger camps where families may need to pay in installments.

3. **Member Photos**: System records photo permissions only. No actual photo storage.

4. **Historical Data**: Starting fresh, no data migration needed.

5. **Attendance Requirements**: No minimum attendance enforced by system. However, system flags full term of absences for leader review. Unexplained absence for full term may result in place forfeiture (leader discretion).

6. **Communication**: System generates BCC-ready email lists for various groups. Supports multiple email addresses per girl (for split families). Actual email sending done externally.

---

## Development Approach

### Phase 1: Foundation (Weeks 1-2)
- Set up database with encryption
- Implement authentication
- Create Person management with multiple emergency contacts
- Data removal functionality for leaving members
- Basic UI with branding

### Phase 2: Meetings (Weeks 3-4)
- Meeting management (CRUD)
- Attendance tracking with consent tracking (email + form)
- Term configuration
- Attendance monitoring and alerts

### Phase 3: Payments (Weeks 5-6)
- Payment tracking with partial payment support
- Termly subs generation
- Activity cost handling
- Payment management UI

### Phase 4: Communications & Polish (Week 7)
- Email list generation
- Dashboard with key metrics and alerts
- Reporting and lists
- Testing and refinement
- Documentation

---

*Document Version: 1.1*
*Last Updated: 2026-01-03*
*Status: Ready for Implementation*

---

## Notes

- System designed with data protection as priority
- Foreign key relationships use MembershipNumber for persistence after data removal
- Multiple emergency contacts support modern family structures
- Consent tracking workflow supports real-world parent behavior
- Partial payments essential for accessibility and larger events
- Email generation reduces manual work while keeping actual sending in leader's control
