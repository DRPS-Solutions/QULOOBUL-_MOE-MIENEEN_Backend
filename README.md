# QULOOBUL _MOE'MIENEEN_Backend

ASP.NET Core Web API for the Quloobul Moe'mieneen NPO platform - projects, tasks,
resource requests, meetings and file storage for one organisation's staff and volunteers.
Serves both clients in this solution: a Blazor WebAssembly web app and a .NET MAUI app.

**Status: API built, both clients wired to it, deployed to Azure.** The API has a DbContext,
ten migrations, 12 controllers and 21 services. The web and MAUI clients both sign in against
`AuthController`, read and write through the controllers using the `Shared` DTOs, and take
live updates from the SignalR hub. See [What's not built yet](#whats-not-built-yet).

## Contents

- [Solution layout](#solution-layout)
- [Stack](#stack)
- [Running it](#running-it)
  - [Database](#database)
  - [Seed accounts](#seed-accounts)
  - [Clients](#clients)
- [Data model](#data-model)
  - [Database integrity](#database-integrity)
- [API](#api)
- [Services](#services)
  - [Rules worth knowing](#rules-worth-knowing)
- [Clients](#clients-1)
  - [`Frontend_BlazorWeb` - Blazor WebAssembly](#frontend_blazorweb---blazor-webassembly)
    - [Accessibility](#accessibility)
  - [`Frontend_Mobile` - .NET MAUI](#frontend_mobile---net-maui)
- [CI/CD](#cicd)
- [YouTube Video Link](#youtube-video-link)
- [References](#references)

## Solution layout

| Project | What it is |
|---|---|
| `QULOOBUL _MOE'MIENEEN_Backend/` | The Web API. Controllers → services → EF Core. |
| `QULOOBUL _MOE'MIENEEN_Shared/` | DTOs both clients and the API compile against. `net10.0`. |
| `Frontend_BlazorWeb/` | Blazor **WebAssembly** SPA. Not Blazor Server - see note below. |
| `Frontend_Mobile/` | .NET MAUI app (Android / iOS / MacCatalyst / Windows). |
| `Backend.Tests/` | xUnit unit and integration tests for the service rules, state machines and API. |
| `infra/` | Bicep for one Azure environment (`main.bicep`), deployed once for staging and once for prod. |

`Frontend_BlazorWeb` is WASM on purpose, not by accident: the API's CORS origins match the
WASM template's ports, and `NotificationHub` reads its JWT from the query string, which is
the client-side SignalR pattern. Don't "fix" it to Server.

## Stack

| | |
|---|---|
| Runtime | .NET 10 (`net10.0`), nullable + implicit usings on |
| Data | EF Core 10, SQL Server provider |
| Auth | JWT bearer + 4 permission-claim policies (`ManageUsers`, `ReviewRequests`, `GenerateReports`, `ManageFiles`) |
| Realtime | SignalR `NotificationHub` at `/hubs/notifications`, groups `user-{id}` |
| Files | Azure Blob Storage - Azurite for local development |
| Email | Azure Communication Services (`EmailService`) |
| API docs | OpenAPI (`Microsoft.AspNetCore.OpenApi`), dev environment only |
| Container | Linux, `mcr.microsoft.com/dotnet/aspnet:10.0` |
| Hosting | Azure App Service + Static Web Apps + Azure SQL, South Africa North (POPIA) |
| CI/CD | GitHub Actions → Azure App Service + Static Web Apps; signed APK artifact for Android |

## Running it

The project name contains a space and an apostrophe - quote every path you pass to `dotnet`.

```bash
dotnet run --project "QULOOBUL _MOE'MIENEEN_Backend/QULOOBUL _MOE'MIENEEN_Backend.csproj"
```

`http://localhost:5012`, or `https://localhost:7272` on the `https` profile. OpenAPI is at
`/openapi/v1.json` in Development only; `/health` is anonymous and always mapped.

### Database

The database is LocalDB by default (`ConnectionStrings:Default` in `appsettings.json`). To use
another server, put `ConnectionStrings__Default=...` in a `.env` file next to the API project:
`Program.cs` loads `KEY=VALUE` lines from it into the environment at startup. Every `.env*`
file is gitignored. If LocalDB fails to start on your machine, point `.env` at a SQL Server
Express instance (`Server=.\SQLEXPRESS;...`).

Apply migrations before the first run, and again after pulling a new one:

```bash
dotnet ef database update --project "QULOOBUL _MOE'MIENEEN_Backend/QULOOBUL _MOE'MIENEEN_Backend.csproj"
```

A database missing a migration fails at runtime with `Invalid column name ...`, and the web client
shows "An unexpected error occurred" on sign-in.

### Seed accounts

`DbSeeder` runs via `UseSeeding` after the migration. It seeds roles, permissions and reference
data, and creates accounts only from user-secrets (never from code):

| Secret | Creates |
|---|---|
| `Seed:AdminEmail` + `Seed:AdminPassword` | The first Admin, if the database has no users yet |
| `Seed:DevPassword` | Local testing only: one active account per non-admin role, `<role>@dev.local` (e.g. `staff@dev.local`, `secondaryadmin@dev.local`), all with that password. Never set in Azure. |

```bash
dotnet user-secrets set "Seed:DevPassword" "<a password>" --project "QULOOBUL _MOE'MIENEEN_Backend/QULOOBUL _MOE'MIENEEN_Backend.csproj"
```

### Clients

```bash
dotnet run --project Frontend_BlazorWeb/Frontend_BlazorWeb.csproj
```

The web app runs at `https://localhost:7136` and reads the API address from
`wwwroot/appsettings.json` (`ApiBaseUrl`).

```bash
dotnet build Frontend_Mobile/Frontend_Mobile.csproj -f net10.0-android
```

The MAUI project needs the **.NET Multi-platform App UI development** workload. Configured
targets: `net10.0-android` (minSdk 21), `net10.0-ios` (15), `net10.0-maccatalyst`,
`net10.0-windows10.0.19041.0`.

Build and test the whole solution:

```bash
dotnet build "QULOOBUL _MOE'MIENEEN_Backend.slnx" -c Release -warnaserror
```

```bash
dotnet test Backend.Tests
```

CI builds with `-warnaserror`, so a warning locally is a red build remotely. Stop a running API
before `dotnet test` or `dotnet ef`: it locks the build output.

## Data model

27 entities under `Models/`, one file each, grouped by bounded context, and 27 matching
`DbSet`s on `AppDbContext`. Table names are set explicitly with `[Table]` to match the ERD
(`Task`, `Task_Assignment`, `File_Upload`…), so the C# names and the SQL names differ on purpose.

| Folder | Entities |
|---|---|
| `IdentityAndAccess/` | `User`, `Role`, `Permission`, `RolePermission`, `UserDirectoryEntry` (the `vw_UserDirectory` view) |
| `Project/` | `Project`, `ProjectMember`, `ProjectTask` |
| `Task/` | `TaskAssignment`, `TaskComment`, `TaskSchedule`, `TaskStatusHistory` |
| `Request/` | `ResourceRequest`, `RequestCategory`, `RequestStatusHistory` |
| `Collaboration/` | `Meeting`, `MeetingAttendee`, `MeetingMinutes`, `Event`, `EventAttendee` |
| `File/` | `FileUpload`, `FileCategory` |
| `NotificationsAudit/` | `Notification`, `NotificationType`, `NotificationPreference`, `AuditLog`, `Report` |

Six modelling decisions carry business rules and shouldn't be "tidied away":

- **`ProjectTask`, not `Task`** - avoids colliding with `System.Threading.Tasks.Task`.
  The table stays `Task`.
- **`Role.HierarchyLevel`** - lower number is higher authority. Tasks may be assigned
  down the hierarchy, never up. The rule needs an ordering to compare against.
- **`ProjectTask.PercentComplete`** - 0–100. Progress is partial, not done/not-done.
- **`MeetingMinutes.IsAiDraft` / `ApprovedBy`** - AI-generated minutes are a draft until
  a person approves them. The approval step is in the schema so procedure can't skip it.
- **`ProjectMember`** - project-scoped visibility. A user allocated to one project sees
  only that project; this is the table the authorisation check reads.
- **`RequestStatusHistory.Note`** - the reviewer's reason on an approve or decline. It lives on
  the history row, so the requester can read why on the request itself, not only in a notification.

Delete behaviour is set per relationship: `Cascade` for rows meaningless without their parent
(task comments and assignments, project members, meeting and event attendees), `SetNull` for a
file's task/meeting link, and `NoAction` everywhere else, including every FK to `User` and the
history tables. Integrity beyond the foreign keys (unique pairs, 27 check constraints, indexes,
defaults and concurrency tokens) is listed under [Database integrity](#database-integrity).

Database-side defence in depth (migration `SecurityAndIndexes`):

- **Row-level security** on `Notification`: `SessionContextInterceptor` sets
  `SESSION_CONTEXT('UserId')` read-only on every connection, and the policy filters to that
  user's rows. No user (login, background jobs, migrations) means no filter.
- **`vw_UserDirectory`**: the staff directory reads a view with no password hash or token columns.

Ten migrations: `InitialCreate`, `SecurityAndIndexes`, `TemporaryPassword`, `RequestDecisionNote`, then the
integrity set `AddRangeCheckConstraints`, `AddAllowedValueCheckConstraints`, `AddCrossColumnCheckConstraints`,
`AddNotBlankCheckConstraints`, `AddQueryIndexes` and `AddDefaultsAndRowVersion`.

### Database integrity

Every rule below is enforced by SQL Server itself, so a bad write from any path (a future endpoint, a
script, a manual `UPDATE`) is rejected, not only one that goes through a service. The services still
check the same rules first so the user gets a clear message; the database is the backstop. All of it
is configured in `AppDbContext.OnModelCreating`.

**Check constraints (27)**

| Group | Constraint | Table | Business rule |
|---|---|---|---|
| Range | `CK_Task_PercentComplete` | `Task` | Progress is 0 to 100 |
| Range | `CK_Report_PeriodOrder` | `Report` | A report period ends on or after it starts |
| Range | `CK_User_FailedLoginCount` | `User` | The failed-login counter never goes negative |
| Allowed values | `CK_Task_Status`, `CK_TaskStatusHistory_NewStatus` | `Task`, `Task_Status_History` | Only the SM-01 states: To Do, In Progress, Blocked, Done, Cancelled, Archived |
| Allowed values | `CK_ResourceRequest_Status`, `CK_RequestStatusHistory_NewStatus` | `Resource_Request`, `Request_Status_History` | Only the SM-02 states: Pending, Under Review, Escalated, Approved, Declined, Completed |
| Allowed values | `CK_User_Status` | `User` | Only the SM-03 states: Pending Activation, Active, Locked, Inactive, Anonymised |
| Allowed values | `CK_Task_Priority`, `CK_ResourceRequest_Priority` | `Task`, `Resource_Request` | Low, Normal, Medium or High |
| Allowed values | `CK_Project_Status` | `Project` | Active, Closed or Cancelled |
| Allowed values | `CK_TaskSchedule_DayOfWeek` | `Task_Schedule` | A real day name, as `DayOfWeek.ToString()` writes it |
| Cross-column | `CK_User_ActiveMatchesStatus` | `User` | `IsActive` is true exactly when `Status` is Active (`Lifecycle.MoveUser`) |
| Cross-column | `CK_User_InactiveHasDate` | `User` | An inactive account records when it was deactivated |
| Cross-column | `CK_User_TokenPair` | `User` | The set-password token hash and its expiry are both present or both absent |
| Cross-column | `CK_ResourceRequest_DecisionHasReviewer` | `Resource_Request` | An approved or declined request names who decided it |
| Cross-column | `CK_Task_DoneHasCompletedBy` | `Task` | A finished task records who completed it |
| Cross-column | `CK_FileUpload_OneContext` | `File_Upload` | A file attaches to at most one of task, meeting, or period |
| Non-empty text | `CK_Project_NameNotBlank`, `CK_Task_TitleNotBlank`, `CK_Meeting_TitleNotBlank`, `CK_Event_TitleNotBlank` | `Project`, `Task`, `Meeting`, `Event` | Names and titles are not empty or all spaces |
| Non-empty text | `CK_FileUpload_FileNameNotBlank`, `CK_FileUpload_FilePathNotBlank` | `File_Upload` | A file has a name and a storage path |
| Non-empty text | `CK_User_FirstNameNotBlank`, `CK_User_SurnameNotBlank` | `User` | A user has a first name and surname |
| Format | `CK_User_EmailFormat` | `User` | Email has the shape `something@something.something` |

Priority and project status are free text in the services, so their lists are the values the clients and
seeder use today; add a value to the constraint and the service together.

**Unique constraints and indexes**

| Index | Table | Purpose |
|---|---|---|
| Unique pair per junction table | `Task_Assignment`, `Project_Member`, `Meeting_Attendee`, `Event_Attendee`, `Role_Permission`, `Notification_Preference` | No duplicate membership or assignment |
| `IX_User_Email` (unique) | `User` | One account per email |
| `IX_User_PasswordTokenHash` (unique, filtered to non-null) | `User` | Set-password link lookup by token; no two users share a token |
| `IX_Event_Location_EventDate` (unique, filtered to non-empty location) | `Event` | One event per venue per slot, even under concurrent bookings |
| `(ProjectId, Status)` | `Task` | The task board |
| `(Status, Deadline)`, `(Deadline)` | `Task` | Deadline-warning job; calendar and report date ranges |
| `(Status, DateSubmitted)` | `Resource_Request` | 48-hour escalation job; newest-first list |
| `(UserId, IsRead)` | `Notification` | The unread badge |
| `(NewStatus, TimeCreated)` | `Task_Status_History` | Completed-in-period report |
| `(UserId, Timestamp)`, `(Timestamp)` | `Audit_Log` | A user's recent activity; time-ranged audit queries |
| `(MeetingDate)`, `(EventDate)` | `Meeting`, `Event` | Calendar and clash-check ranges |

EF Core also indexes every foreign-key column, so those are not repeated here.

**Defaults and concurrency**

- `User.Status` defaults to `Active` and `Task.PercentComplete` to `0`, so a row inserted outside EF starts valid.
- `Task` and `Resource_Request` carry a `RowVersion` (`rowversion`) concurrency token. A save whose row
  changed since it was loaded fails, and the API answers 409 "Someone else changed this at the same time".
  The clients do not send the token back, so this guards overlapping requests, not a stale screen.

**Proof.** `QULOOBUL _MOE'MIENEEN_Backend/Data/CheckConstraintTests.sql` tries an invalid write for each
rule inside a transaction it rolls back, plus a valid write as a control, and prints a failure count
(0 = every rule held). It also checks the defaults and that a stale `RowVersion` update matches no rows.

```bash
sqlcmd -S .\SQLEXPRESS -d QuloobulMoeMieneen -E -C -W -i "QULOOBUL _MOE'MIENEEN_Backend/Data/CheckConstraintTests.sql"
```

## API

12 controllers, all delegating to a service - no logic in the controller, per the architecture
doc's layering: `Api.Controller` → `Api.Services` → `Api.DataAccess`.

`Auth`, `Tasks`, `TaskSchedules`, `Projects`, `Requests`, `Meetings`, `Events`, `Notifications`,
`Users`, `Files`, `Reports`, `Search`, plus `ApiControllerBase`.

## Services

21 services under `Services/`, all registered in `Program.cs`. One use-case handler per use
case; the `DbContext` is the unit of work, one `SaveChanges` per operation.

| Group | Services |
|---|---|
| Use cases | `TaskService`, `TaskAssignmentService`, `TaskScheduleService`, `ProjectService`, `RequestReviewService`, `MeetingMinutesService`, `EventService`, `UserService`, `StaffOverviewService`, `ReportingService`, `SearchService` |
| Auth & access | `AuthService`, `AccessGuard` |
| Files | `FileUploadService`, `FileManagementService`, `BlobStorageService` |
| Notifications | `NotificationService`, `NotificationQueryService`, `EmailService` |
| Cross-cutting | `AuditService`, `MaintenanceJobs` |

`MaintenanceJobs` is the one `IHostedService`: SM-02's 48-hour escalation, SM-01's deadline
reminders, and `Task_Schedule`'s recurring-task generation - the transitions driven by a clock
rather than a user action. It sweeps hourly; each sweep is idempotent. The same sweep deletes any
attachment bigger than `Files:MaxBytes` (and its metadata row): a SAS cannot cap what the holder
writes, so the declared size is only checked up front.

`AccessGuard` is the object-level authorisation check (the anti-IDOR requirement), called from
every service that reads or changes a user's work. It reads permissions from the database, not
the JWT, so a revoked grant takes effect immediately. File access goes by `ManageFiles` (Admin and
Secondary Admin reach every file); everything else that says "admin" means `ManageUsers`.

State machines SM-01 (`ProjectTask`), SM-02 (`ResourceRequest`) and SM-03 (`User`) live in
`Data/Lifecycle.cs` as guarded transition tables, checked server-side.

### Rules worth knowing

- **Sign-in.** An account locks after 5 consecutive failed sign-ins (`AuthService.MaxFailedLogins`)
  and the admins are notified. The lock lifts itself after 15 minutes (`AuthService.LockoutPeriod`,
  read from the `user.locked` audit row) so a stranger cannot lock the sole admin out for good; an
  admin's reset still unlocks it at once. New accounts get an emailed set-password link. An admin's Reset
  Password issues a temporary password, shown once; it unlocks only the change-password route
  until the user picks their own. Every sign-in failure (unknown email, wrong password, pending,
  locked or deactivated) gets the same message (`AuthService.LoginFailed`), so nobody can probe which
  emails have accounts. Setting a new password ends every older token (a stolen session does not
  survive it): the token's `iat` is compared with the `user.password.set` audit row in `Program.cs`,
  and `POST api/users/me/password` returns a fresh `AuthResponse` for the caller to carry on with.
  `GET api/users` gives colleagues names and roles only; the login email and exact account status
  (Pending, Locked) go to `ManageUsers` holders.
- **Forgot password** notifies the admins rather than emailing a link. The notification shows the
  account's own name, and flags it when the name typed on the form doesn't match.
- **Deactivate / reactivate** (SM-03 Active ↔ Inactive). Deactivating reassigns the user's open tasks.
- **Recurring tasks** (`TaskSchedule`) are weekly, by day of the week, and assigned strictly down
  the hierarchy like daily tasks. The creator or an admin can pause one.
- **Event Cancellation** (`POST api/projects/{id}/cancel`, lead or admin). Event Planning's events
  are projects. Cancelling keeps the event and its history, cancels its To Do and In Progress
  tasks, and notifies its staff with the reason. Blocked tasks are left alone: SM-01 has no
  Blocked → Cancelled edge.
- **Double booking.** Calendar events refuse a second booking of the same location and time, and
  nobody can be booked into two events or meetings at the same time. Meetings have no location
  (the architecture doc: an event is "distinguished from a meeting by having a location").
- **Reports** cover one year at most, and a period with no data asks for a wider range.
- **Downloads.** File download links are short-lived SAS URLs with `Content-Disposition: attachment`,
  so the browser saves the file under its original name instead of opening it.

## Clients

Both front ends authenticate against `AuthController` and keep no data of their own: every
list is fetched from the API, and a SignalR push tells them which list to refetch.

### `Frontend_BlazorWeb` - Blazor WebAssembly

Ported from the Blazor Server + MudBlazor prototype; the `@rendermode InteractiveServer`
directives were stripped, since they're meaningless in standalone WASM. `AppDataService.cs` is
the data layer: it signs in, holds the lists several pages share, sends every HTTP call through
one error path, and listens on the hub. It is scoped, which in WASM means one instance per tab.

| Piece | Where |
|---|---|
| Shell, login, change-password gate, theme | `Layout/MainLayout.razor` |
| Role-gated nav, bell, settings and account menus | `Layout/NavMenu.razor` |
| Role matrix (which pages a role sees) | `AppDataService.CanSee*` |
| Pages | `Pages/` - Home, Daily Tasks (with Recurring Tasks), Resource Requests, Meetings, User Management, Event Planning, Calendar, Files, Reports, Notifications, Search, Profile, Set/Forgot Password |

What it keeps in the browser, and why:

- **The session in `sessionStorage`.** It survives a reload but not closing the tab, so a shared
  computer at the NPO doesn't stay signed in. An expired token is discarded on load.
- **Dark mode and nav layout in `localStorage`**, per browser, not per account.
- **Vertical nav is desktop only** (900px and up). On a phone a 260px sidebar leaves no room for
  the page, so it falls back to the hamburger menu; below 480px the header shows the logo only.

The hub connection takes its token from the `HttpClient`'s auth header, not `CurrentUser`:
`CurrentUser` is only set after the session (and the hub) has started.

#### Accessibility

MudBlazor gives ARIA roles and keyboard handling on its own components; the work below covers
what its defaults don't: the hand-built `drps-*` cards and chips, the board, focus, and colour.

| Area | What the web client does |
|---|---|
| Keyboard reach | Clickable cards, list rows, filter chips and toggles are `role="button" tabindex="0"`; one handler in `wwwroot/index.html` turns Enter/Space into a click. Filter chips use `aria-pressed`, selected rows `aria-current`. Task cards hold other controls, so only the title is the button (a button can't contain buttons). |
| Focus | A 3px focus ring on everything focusable, blue on light surfaces and light blue on the dark navbar and in dark mode (`wwwroot/css/app.css`). A "Skip to main content" link is the first Tab stop. Dialogs focus their first field, close on Escape and return focus to what opened them. `FocusOnNavigate` lands on each page's `<h1>`. |
| Board | The Kanban drag-and-drop has a keyboard route: each card's ⋮ menu ("Move to ...") calls the same `MoveCard` as a drop, so the same step-skipping rules apply. |
| Names and announcements | Every icon-only button has an `aria-label` (file name or note included where there are many). The login error and "no events planned" hints are live regions; snackbars already are. |
| Colour | Text meets WCAG 2.1 AA (4.5:1) in light and dark mode, below. Reduced-motion is honoured (`prefers-reduced-motion`). |

Contrast, checked with the WCAG formula and re-checked in the running app. The palette changes
are in the `_theme` in `MainLayout.razor`, the rest in each page's `<style>` block.

| Text | Before | After |
|---|---|---|
| Label on a filled dark-mode button (green) | 2.72 | 6.48 (dark ink) |
| Dark-mode Secondary text/outline on a card | 2.57 | 7.37 |
| Dark-mode Info on a card | 3.53 | 5.86 |
| Dark-mode empty-state text | 1.91 | 4.69 |
| Dark-mode muted text on a task card | 4.36 | 6.44 |
| Light-mode muted text (`#75806F`) | 3.63 - 4.14 | 5.20 - 5.77 |
| Light-mode empty-state text | 2.12 | 4.69 |
| White on the red unread badge | 3.68 | 5.62 |
| Warning (orange) text on white | 2.15 | 5.86 |
| "Completed" chip text | 2.99 | 4.66 |

Automated check: axe-core 4.10.2 (WCAG 2.0/2.1 A and AA rules), run in the app signed in as a
Facility Manager on every page, in light mode, before (commit `6256521`) and after. Counts are
elements failing, not rules.

| Page | Before | After |
|---|---|---|
| Home | 4 | 0 |
| Daily Tasks, day view | 31 | 0 |
| Daily Tasks, board view | 14 | 0 |
| Resource Requests | 26 | 0 |
| Meetings | 2 | 0 |
| Event Planning | 8 | 0 |
| Calendar | 5 | 0 |
| Files | 7 | 0 |
| Reports | 3 | 0 |
| Notifications | 14 | 0 |
| Profile | 4 | 0 |
| Search | 3 | 0 |
| **Total** | **121** | **0** |

User Management (`/users`) was not in the before run; after the fixes it is 0 too. Dark mode was
run after only: 0 on every page. The "before" failures were mostly low-contrast text, plus the
unlabelled navbar menu buttons on every page and the unlabelled file and category inputs on Files.

Tested by hand with the keyboard only: skip link (Tab, Enter lands in the page), a card's
Enter/Space, the ⋮ menu moving a task In Progress to Blocked, and New task / Task details opening
on their first field, closing on Escape and returning focus. Not done: a screen reader pass,
Lighthouse, keyboard use of every dialog, and the MAUI app, none of which axe covers. Automated
tools find only part of the problems, so treat 0 as "no machine-detectable failures", not "accessible".

### `Frontend_Mobile` - .NET MAUI

Ported from the standalone MAUI app at `C:\3rdYear\XBCAD\Mobile`, which was itself a port of
the Kotlin/Compose Android prototype, itself a port of the Blazor/MudBlazor original. Same
screens, same palette, same role gating at each step.

| Piece | Where |
|---|---|
| Navigation shell, role-gated flyout, unread-count bell, dark-mode toggle | `AppShell.xaml` |
| Screens | `Pages/` - Login, Home, Tasks, TaskDetails, CreateTask, Requests, Events, Meetings, Calendar, Files, Reports, Notifications, Search, UserManagement |
| Session, API calls, SignalR hub connection (the web client's `AppDataService`, ported) | `Data/AppState.cs` |
| Read-only view models over the `Shared` DTOs, with display-formatting properties (so the XAML needs no converters) | `Models/Models.cs` |
| Palette and styles, light/dark via `AppThemeBinding` | `Resources/Styles/` |

Five things about it are deliberate, not oversights:

- **Icons are Unicode glyphs, not Material Icons.** MAUI doesn't bundle an icon font and
  adding one wasn't worth the dependency. Swap a Material Icons `.ttf` into `Resources/Fonts`
  and change the `Glyph` properties in `Models.cs` for exact parity.
- **Ticking a task completes it as the signed-in user**, as on the web: the API records who.
  SM-01 has no To Do → Done edge, so a tick sends two guarded moves (→ In Progress → Done).
  The deny reason is a native `DisplayPromptAsync`. New Task is a full modal page instead,
  because no MAUI inline dialog fits a form that size.
- **Events are API projects**, as on the web client: coordinator = owner, staff = members.
  Projects have no date, location, budget, capacity or resource list, so those fields were
  dropped from the form rather than kept as inputs that save nowhere.
- **The API address is compiled in** (`AppState.ApiBase`): Debug builds use the local API
  (`https://10.0.2.2:7272/` on the Android emulator, `https://localhost:7272/` elsewhere) and
  trust the ASP.NET dev certificate; `-p:ApiEnvironment=Staging` builds use the staging App
  Service; Release builds use production. The session token is kept in `SecureStorage` and
  resumed on the next launch while it's still valid.
- **Login is a Shell route, not a separate root page.** Swapping the window root between a
  `ContentPage` and a `Shell` crashes Android's fragment manager
  ([dotnet/maui#21257](https://github.com/dotnet/maui/issues/21257)), so the Shell owns the
  whole session and navigates between `//login` and `//home`.

> **Android gotcha:** changing `<ApplicationId>` leaves the old package's R classes in `obj`,
> and the mismatched resource IDs surface as runtime crashes pointing nowhere near the cause
> ([dotnet/maui#31703](https://github.com/dotnet/maui/issues/31703)). Clear `bin`/`obj` and
> reinstall fresh after any ID change.

## CI/CD

Four workflows in `.github/workflows/`:

| Workflow | Trigger | Does |
|---|---|---|
| `ci.yml` | every branch except deploy branches, + PRs into them | restore, build, test, upload coverage. Holds no credentials. |
| `cd-staging.yml` | push to `develop` / `dev` | build → deploy API to the staging app and the web client to Static Web Apps → EF migrations → smoke test |
| `cd-production.yml` | push to `main` / `master` | build → deploy to the prod staging slot → migrations → smoke test → **manual-approval swap** → web client |
| `cd-mobile.yml` | changes under `Frontend_Mobile/**` or `Shared/**` on a deploy branch, or a manual run | Android Release build → signed APK artifact (14-day retention). `dev`/`develop` builds talk to the staging API. |

Quality gates, in every build job:

- **Zero warnings.** `Directory.Build.props` sets `TreatWarningsAsErrors` for every project,
  so a local build fails the same way CI does.
- **Unit and integration tests.** `ApiIntegrationTests` boots the real API with
  `WebApplicationFactory`, migrates a throwaway SQL Server database from empty, and drops it
  afterwards. Locally that is LocalDB unless `TEST_SQL_CONNECTION` points elsewhere; in CI it's
  a `mssql` service container.
- **Coverage threshold.** `.github/scripts/coverage-gate.sh 70` fails the job below 70% line coverage.
- **Smoke test after deploy.** `.github/scripts/smoke-test.sh` checks `/health`, signs in as
  `SMOKE_EMAIL`, and makes one authenticated read, before any slot swap.

Migrations are applied by the CD workflows (the runner opens the SQL firewall for itself, migrates,
and closes it again), so a new migration reaches staging on the next push to `dev`. Staging migrations
run against the database the live build is still using, so **every migration must be backward compatible**.

The API sends `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` and is rate
limited (300 requests/min per user, 10/min per IP on the anonymous auth routes; both under
`RateLimit:*`). The web client is published in the build job and deployed prebuilt:
`.github/scripts/web-config.sh` points its `appsettings.json` at the API and writes
`staticwebapp.config.json`, whose CSP allows Blazor's inline import map by hash (it changes per build).

Production deploys to a slot and swaps only after approval, configured as required reviewers
on the `production` GitHub environment. Roll back by running the same swap again.

Required repository config:

| Kind | Name |
|---|---|
| Variables | `AZURE_WEBAPP_NAME`, `AZURE_WEBAPP_NAME_PROD`, `AZURE_RESOURCE_GROUP`, `AZURE_STATIC_WEB_APP*` |
| Secrets | `AZURE_CREDENTIALS`, `STAGING_DB_CONNECTION`, `PRODUCTION_DB_CONNECTION`, `AZURE_STATIC_WEB_APPS_API_TOKEN*`, `SMOKE_EMAIL`, `SMOKE_PASSWORD` (a low-privilege account), `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`, `ANDROID_KEYSTORE_PASSWORD` |
| Azure config | API `Cors:Origins` must list the Static Web Apps URL; the storage account's CORS must allow `PUT`/`GET` from it, since browsers upload and download files straight to Blob Storage. |

Deploy jobs are skipped while `vars.AZURE_WEBAPP_NAME` / `AZURE_WEBAPP_NAME_PROD` are unset.
Mobile builds unsigned and warns when the keystore secret is absent. Play Store distribution
is deliberately not automated - Phase 1 side-loads the APK.

## YouTube Video Link
- https://youtu.be/5JoQesAxa2o

## References

References are kept in the code, not in this README. Each one is a comment next to the code it
supports (C#, Razor, Bicep and JavaScript files), in IEEE style:

```
// Reference: Microsoft, "Integration tests in ASP.NET Core." Accessed: Aug. 08, 2026. [Online]. Available: https://learn.microsoft.com/...
```

To list them all, search the solution for `Reference:`.
