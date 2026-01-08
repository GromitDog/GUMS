# Phase 2 Implementation Plan: Meetings Management

**Created:** 2026-01-05
**Status:** PLANNING
**Estimated Duration:** 4-6 development sessions

---

## Phase 2 Overview

Phase 2 focuses on **Meetings Management** - allowing leaders to plan weekly meetings, track attendance, manage consent forms, and handle meeting costs.

### Goals

By the end of Phase 2, leaders should be able to:
1. Configure term dates for automatic meeting generation
2. Create and manage regular weekly meetings
3. Create special events/camps (extra meetings with costs)
4. Track attendance with quick entry interface
5. Manage consent forms (email confirmation + physical form)
6. Monitor attendance patterns and flag absences
7. Link meeting costs to payment system (foundation for Phase 3)

---

## Requirements from SPECIFICATION.md

### 2.1 Meeting Types

**Regular Meetings:**
- Weekly meetings on the same day of the week
- Follow school term patterns
- Automatically suggested based on term dates

**Extra Meetings:**
- Special events, trips, camps
- May have associated costs
- May require consent forms

### 2.2 Meeting Information

**Basic Details:**
- Date, Start Time, End Time (required)
- Meeting Type (Regular/Extra)
- Title/Description

**Location:**
- Location Name, Address
- Defaults to configured "home location" for regular meetings

**Activities:**
- List of planned activities
- Flag if activity requires consent form
- Notes for each activity

**Costs:**
- Cost per attendee (for extra meetings)
- Supports partial payments for larger amounts (camps)
- Payment deadline

### 2.3 Attendance Tracking

- Mark which members attended each meeting
- Quick entry interface for regular meetings (checklist of active members)
- For extra meetings, track sign-ups vs actual attendance
- Flag for follow-up: full term of absences without prior discussion

### 2.4 Consent Forms

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

## Database Schema Extensions

### New/Modified Services Needed

1. **TermService**
   - Create, Read, Update, Delete terms
   - Get current term
   - Get all terms
   - Validate term dates don't overlap

2. **MeetingService**
   - Create, Read, Update, Delete meetings
   - Get meetings by date range
   - Get upcoming meetings
   - Get meetings for a specific term
   - Generate regular meetings based on term dates
   - Track activities within meetings

3. **AttendanceService**
   - Record attendance for a meeting
   - Get attendance for a specific meeting
   - Get attendance history for a member
   - Detect attendance issues (full term absences)
   - Track consent status (email + physical form)

---

## Implementation Steps

### Step 1: Term Management (Foundation)

**Service Layer:**
- [ ] Create `ITermService` interface
- [ ] Implement `TermService`
  - CRUD operations for terms
  - Validation (no overlapping dates)
  - Get current term based on today's date
- [ ] Write unit tests for `TermService`

**UI Layer:**
- [ ] Create `Components/Pages/Configuration/TermManagement.razor`
  - List all terms (past, current, future)
  - Add new term
  - Edit existing term
  - Delete term (with validation - can't delete if meetings exist)
  - Highlight current term

**Navigation:**
- [ ] Add "Settings" or "Configuration" menu item
- [ ] Add "Term Dates" sub-menu

**Estimated:** 1 session

---

### Step 2: Meeting Creation & Management (Core Feature)

**Service Layer:**
- [ ] Create `IMeetingService` interface
- [ ] Implement `MeetingService`
  - CRUD operations for meetings
  - CRUD operations for activities within meetings
  - Generate regular meetings for a term
  - Calculate suggested meeting dates based on term + day of week
- [ ] Write unit tests for `MeetingService`

**UI Layer - Meeting Pages:**
- [ ] Create `Components/Pages/Meetings/Index.razor`
  - Calendar view of meetings (monthly)
  - List view (upcoming meetings)
  - Filter by meeting type (Regular/Extra)
  - "Plan a meeting" and "Add special event" buttons

- [ ] Create `Components/Pages/Meetings/AddRegularMeeting.razor`
  - Date picker with suggested dates (based on term + unit's meeting day)
  - Start/End time (default from configuration)
  - Location (default from configuration, editable)
  - Activities list (add/remove)
  - Mark activities as requiring consent

- [ ] Create `Components/Pages/Meetings/AddExtraMeeting.razor`
  - All fields from regular meeting
  - Plus: Cost per attendee
  - Payment deadline
  - More emphasis on consent forms

- [ ] Create `Components/Pages/Meetings/EditMeeting.razor`
  - Edit existing meeting (works for both types)
  - Cannot change past meetings (warning)

- [ ] Create `Components/Pages/Meetings/ViewMeeting.razor`
  - Display meeting details
  - Show activities with consent requirements
  - Link to attendance page (if meeting date has passed)
  - Link to payment tracking (if costs associated)

**Navigation:**
- [ ] Add "Meetings" menu item
- [ ] Update home page with "Upcoming Meetings" widget

**Estimated:** 2 sessions

---

### Step 3: Attendance Tracking (Critical Feature)

**Service Layer:**
- [ ] Create `IAttendanceService` interface
- [ ] Implement `AttendanceService`
  - Record attendance (bulk update for a meeting)
  - Track consent status (email received, form received, dates)
  - Get attendance for a meeting
  - Get attendance history for a member
  - Detect attendance patterns (full term absences)
  - Generate attendance alerts

**UI Layer:**
- [ ] Create `Components/Pages/Meetings/RecordAttendance.razor`
  - Quick checklist for regular meetings
  - Shows all active girls + leaders
  - Checkbox for attended/not attended
  - For extra meetings: show who signed up vs who attended
  - For activities requiring consent: track email + physical form status
  - Save button (bulk save all attendance)

- [ ] Create `Components/Shared/ConsentTracker.razor`
  - Reusable component for tracking consent
  - Radio buttons: Not Received / Email Received / Form Received
  - Date pickers for email date and form date
  - Warning icon if form overdue

- [ ] Add attendance section to `ViewMeeting.razor`
  - Summary: X/Y attended
  - Link to record/edit attendance

**Navigation:**
- [ ] Add "Record Attendance" link from meeting view
- [ ] Add "Attendance" sub-menu under Meetings

**Estimated:** 1-2 sessions

---

### Step 4: Attendance Monitoring & Alerts

**Service Layer:**
- [ ] Extend `AttendanceService`
  - Method to detect full-term absences
  - Method to flag members needing follow-up
  - Method to get attendance statistics

**UI Layer:**
- [ ] Create `Components/Pages/Meetings/AttendanceAlerts.razor`
  - List members with full-term absences
  - Option to add notes (illness, family circumstances)
  - Mark as "acknowledged" to remove from alert list

- [ ] Add attendance alerts to home dashboard
  - Warning badge if any members have full-term absences

**Navigation:**
- [ ] Add "Attendance Alerts" under Meetings menu
- [ ] Add alert icon to home page

**Estimated:** 0.5-1 session

---

### Step 5: Integration & Polish

**Integration:**
- [ ] Link meetings with costs to Payment system (prepare for Phase 3)
  - When extra meeting with cost is created, flag for payment tracking
  - Don't implement full payment yet, just the link

- [ ] Update configuration service to support:
  - Default meeting location (already have in entity, ensure it's editable)
  - Default meeting times (already have)
  - Meeting day of week (already have)

**UI Polish:**
- [ ] Apply Girl Guiding branding to all new pages
- [ ] Ensure friendly tone of voice throughout
- [ ] Responsive design for all new pages
- [ ] Add helpful icons (calendar, checkmark, warning, etc.)

**Testing:**
- [ ] Manual testing of complete meeting workflow
  - Create term
  - Generate regular meetings
  - Add extra meeting with cost
  - Record attendance
  - Track consent forms
  - Check attendance alerts

**Documentation:**
- [ ] Update PROGRESS.md with Phase 2 completion
- [ ] Create PHASE2_AUDIT.md
- [ ] Update README with Phase 2 features

**Estimated:** 0.5-1 session

---

## Total Implementation Breakdown

| Step | Description | Estimated Sessions |
|------|-------------|-------------------|
| 1 | Term Management | 1 |
| 2 | Meeting Creation & Management | 2 |
| 3 | Attendance Tracking | 1-2 |
| 4 | Attendance Monitoring & Alerts | 0.5-1 |
| 5 | Integration & Polish | 0.5-1 |
| **Total** | | **5-7 sessions** |

---

## User Journeys to Support

### Journey 1: Planning a Regular Meeting

1. Leader navigates to **Meetings** → **Plan a Meeting**
2. System suggests next available date based on term calendar and unit's meeting day
3. Leader confirms date (or picks different date)
4. Start/End times pre-filled from configuration (editable)
5. Location pre-filled from configuration (editable)
6. Leader adds activities planned: "Badges", "Games", "Story time"
7. Leader saves meeting
8. Meeting appears in calendar view

### Journey 2: Adding a Camp (Extra Meeting with Consent)

1. Leader navigates to **Meetings** → **Add Special Event**
2. Leader enters:
   - Title: "Summer Camp"
   - Date: 15-17 August
   - Location: "Woodland Adventure Centre, Forest Road"
   - Cost: £60 per girl
   - Payment deadline: 1 August
3. Leader adds activities:
   - "Camping overnight" - **Requires consent ✓**
   - "Hiking" - **Requires consent ✓**
   - "Campfire cooking"
4. Leader saves meeting
5. System flags that consent forms are needed
6. (Phase 3: System will also create payment dues)

### Journey 3: Recording Attendance for Regular Meeting

1. Leader navigates to upcoming meeting
2. Clicks "Record Attendance"
3. Quick checklist shows:
   - **Girls:**
     - [ ] Alice Smith
     - [ ] Bella Jones
     - [ ] Charlie Brown
   - **Leaders:**
     - [ ] Sarah Leader
     - [ ] Emma Assistant
4. Leader checks boxes for who attended
5. Saves attendance
6. Meeting marked as "Attendance Recorded"

### Journey 4: Tracking Camp Consent Forms

1. Leader navigates to "Summer Camp" meeting
2. Views attendance/consent tracker
3. For each girl who signed up:
   - Alice Smith:
     - Parent emailed: Yes → Mark "Email Received" → Enter date: 10 July
     - Form returned: Yes → Mark "Form Received" → Enter date: 15 July
   - Bella Jones:
     - Parent emailed: Yes → Mark "Email Received" → Enter date: 12 July
     - Form returned: Not yet → System flags with warning icon
4. Leader can see at a glance: "1 form outstanding"
5. Leader can generate email list for follow-up (Phase 4)

### Journey 5: Reviewing Attendance Alerts

1. Leader sees warning badge on home page: "2 attendance alerts"
2. Navigates to **Meetings** → **Attendance Alerts**
3. System shows:
   - "Alice Smith has not attended any meetings this term (Autumn 2026)"
   - "Charlie Brown has attended 1/8 meetings this term"
4. Leader clicks on Alice, adds note: "Family illness - spoke to parent on 15 Sept"
5. Marks as acknowledged
6. Alert removed from list

---

## Success Criteria

Phase 2 is complete when:

1. ✅ Leaders can configure term dates
2. ✅ System can generate regular meeting dates automatically
3. ✅ Leaders can create extra meetings with costs and consent requirements
4. ✅ Leaders can record attendance in under 1 minute for regular meetings
5. ✅ Consent tracking (email + physical form) is functional
6. ✅ System flags members with full-term absences
7. ✅ All UI follows Girl Guiding branding
8. ✅ Meetings appear in calendar view
9. ✅ All Phase 2 features have unit tests
10. ✅ Documentation is updated

---

## Technical Considerations

### Database Migrations

- Will need new migration after implementing Meeting, Activity, Attendance entities
- Term entity already exists, may need modification
- Ensure MembershipNumber (not PersonId) is used in Attendance table (already designed this way)

### Performance

- Expected dataset: 40-100 meetings/year, 500-2000 attendance records/year
- Performance should not be a concern at this scale
- Standard EF Core queries will be sufficient

### UI/UX Considerations

- **Calendar view**: Use a simple table-based monthly calendar (no fancy JS library needed)
- **Quick attendance entry**: Critical for adoption - must be fast and easy
- **Consent tracking**: Clear visual indicators (icons, colors) for status
- **Mobile-friendly**: Leaders may record attendance on tablets at meetings

### Integration Points

- **Phase 3 (Payments)**: When extra meeting with cost is created, will need to generate payment dues for attendees
- **Phase 4 (Communications)**: Email lists for parents with outstanding consent forms

---

## Open Questions / Decisions Needed

1. **Should we allow editing of past meetings?**
   - Recommendation: Allow editing attendance/consent, but warn if changing meeting details

2. **How to handle girls joining mid-term?**
   - Recommendation: Only show them in attendance for meetings after their DateJoined

3. **What defines "full-term absence"?**
   - Recommendation: Attended 0 meetings in current term AND term is > 50% complete

4. **Should leaders see attendance percentage?**
   - Recommendation: Yes, show "Attended X/Y meetings this term (Z%)" on member view page

5. **Calendar view: Monthly or weekly?**
   - Recommendation: Monthly view with option to filter by week

---

## Phase 2 Deliverables Summary

**Services:**
- TermService (with interface and tests)
- MeetingService (with interface and tests)
- AttendanceService (with interface and tests)

**Pages:**
- Configuration/TermManagement.razor
- Meetings/Index.razor (calendar + list view)
- Meetings/AddRegularMeeting.razor
- Meetings/AddExtraMeeting.razor
- Meetings/EditMeeting.razor
- Meetings/ViewMeeting.razor
- Meetings/RecordAttendance.razor
- Meetings/AttendanceAlerts.razor

**Components:**
- Shared/ConsentTracker.razor

**Database Migration:**
- Migration to modify Term, Meeting, Activity, Attendance entities if needed

**Documentation:**
- PROGRESS.md updated
- PHASE2_AUDIT.md created
- README.md updated

**Tests:**
- TermServiceTests (full coverage)
- MeetingServiceTests (full coverage)
- AttendanceServiceTests (full coverage)

**Estimated Total:**
- ~8 new/modified pages
- 3 new services with interfaces
- 3 new test suites
- 1 shared component
- ~2,000+ lines of code

---

## Next Steps to Begin Phase 2

1. **Review and approve this plan**
2. **Start with Step 1: Term Management**
   - Create TermService interface
   - Implement TermService
   - Write tests
   - Create TermManagement UI
3. **Test term management thoroughly before proceeding**
4. **Move to Step 2: Meeting Creation**

---

**End of Phase 2 Plan**

This plan provides a clear roadmap for implementing all meeting management features in a logical, incremental manner. Each step builds on the previous one, ensuring the foundation is solid before adding complexity.
