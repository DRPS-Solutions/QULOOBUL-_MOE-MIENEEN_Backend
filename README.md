# QULOOBUL _MOE'MIENEEN_Backend

ASP.NET Core Web API for the Quloobul Moe'mieneen NPO platform — projects, tasks,
resource requests, meetings and file storage for one organisation's staff and volunteers.
Serves both clients in this solution: a Blazor WebAssembly web app and a .NET MAUI app.

**Status: API built, both clients wired to it.** The API has a DbContext, migrations,
10 controllers and 19 registered services. The web and MAUI clients both sign in against
`AuthController`, read and write through the controllers using the `Shared` DTOs, and take
live updates from the SignalR hub. See [What's not built yet](#whats-not-built-yet).

## Solution layout

| Project | What it is |
|---|---|
| `QULOOBUL _MOE'MIENEEN_Backend/` | The Web API. Controllers → services → EF Core. |
| `QULOOBUL _MOE'MIENEEN_Shared/` | DTOs both clients and the API compile against. `net10.0`. |
| `Frontend_BlazorWeb/` | Blazor **WebAssembly** SPA. Not Blazor Server — see note below. |
| `Frontend_Mobile/` | .NET MAUI app (Android / iOS / MacCatalyst / Windows). |
| `Backend.Tests/` | xUnit tests for the service rules and state machines. |

`Frontend_BlazorWeb` is WASM on purpose, not by accident: the API's CORS origins match the
WASM template's ports, and `NotificationHub` reads its JWT from the query string, which is
the client-side SignalR pattern. Don't "fix" it to Server.

## Stack

| | |
|---|---|
| Runtime | .NET 10 (`net10.0`), nullable + implicit usings on |
| Data | EF Core 10, SQL Server provider |
| Auth | JWT bearer + 4 permission-claim policies |
| Realtime | SignalR `NotificationHub` at `/hubs/notifications`, groups `user-{id}` |
| Files | Azure Blob Storage — Azurite for local development |
| API docs | OpenAPI (`Microsoft.AspNetCore.OpenApi`), dev environment only |
| Container | Linux, `mcr.microsoft.com/dotnet/aspnet:10.0` |
| CI/CD | GitHub Actions → Azure App Service (slot swap) + Static Web Apps |

## Running it

The project name contains a space and an apostrophe — quote every path you pass to `dotnet`.

```bash
dotnet run --project "QULOOBUL _MOE'MIENEEN_Backend/QULOOBUL _MOE'MIENEEN_Backend.csproj"
```

`http://localhost:5012`, or `https://localhost:7272` on the `https` profile. OpenAPI is at
`/openapi/v1.json` in Development only; `/health` is anonymous and always mapped.

The database is LocalDB by default (`ConnectionStrings:Default` in `appsettings.json`).
Apply migrations before the first run:

```bash
dotnet ef database update --project "QULOOBUL _MOE'MIENEEN_Backend/QULOOBUL _MOE'MIENEEN_Backend.csproj"
```

`DbSeeder` runs via `UseSeeding` after the migration, so the first run comes up populated.

Clients:

```bash
dotnet run --project Frontend_BlazorWeb/Frontend_BlazorWeb.csproj
```

```bash
dotnet build Frontend_Mobile/Frontend_Mobile.csproj -f net10.0-android
```

The MAUI project needs the **.NET Multi-platform App UI development** workload. Configured
targets: `net10.0-android` (minSdk 21), `net10.0-ios` (15), `net10.0-maccatalyst`,
`net10.0-windows10.0.19041.0`.

Build the whole solution:

```bash
dotnet build "QULOOBUL _MOE'MIENEEN_Backend.slnx" -c Release -warnaserror
```

CI builds with `-warnaserror`, so a warning locally is a red build remotely.

## Data model

25 entities under `Models/`, one file each, grouped by bounded context, and 25 matching
`DbSet`s on `AppDbContext`. Table names are set explicitly with `[Table]` to match the ERD
(`Task`, `Task_Assignment`, `File_Upload`…), so the C# names and the SQL names differ on purpose.

| Folder | Entities |
|---|---|
| `IdentityAndAccess/` | `User`, `Role`, `Permission`, `RolePermission` |
| `Project/` | `Project`, `ProjectMember`, `ProjectTask` |
| `Task/` | `TaskAssignment`, `TaskComment`, `TaskSchedule`, `TaskStatusHistory` |
| `Request/` | `ResourceRequest`, `RequestCategory`, `RequestStatusHistory` |
| `Collaboration/` | `Meeting`, `MeetingAttendee`, `MeetingMinutes`, `Event`, `EventAttendee` |
| `File/` | `FileUpload`, `FileCategory` |
| `NotificationsAudit/` | `Notification`, `NotificationType`, `AuditLog`, `Report` |

Five modelling decisions carry business rules and shouldn't be "tidied away":

- **`ProjectTask`, not `Task`** — avoids colliding with `System.Threading.Tasks.Task`.
  The table stays `Task`.
- **`Role.HierarchyLevel`** — lower number is higher authority. Tasks may be assigned
  down the hierarchy, never up. The rule needs an ordering to compare against.
- **`ProjectTask.PercentComplete`** — 0–100. Progress is partial, not done/not-done.
- **`MeetingMinutes.IsAiDraft` / `ApprovedBy`** — AI-generated minutes are a draft until
  a person approves them. The approval step is in the schema so procedure can't skip it.
- **`ProjectMember`** — project-scoped visibility. A user allocated to one project sees
  only that project; this is the table the authorisation check reads.

Delete behaviour is set per relationship: `Cascade` for rows meaningless without their parent
(task comments and assignments, project members, meeting and event attendees), `SetNull` for a
file's task/meeting link, and `NoAction` everywhere else, including every FK to `User` and the
history tables. Unique pairs, a `CK_FileUpload_OneContext` check constraint, and indexes on the
task board (`ProjectId, Status`), the unread badge (`UserId, IsRead`) and `Audit_Log.Timestamp`.

Database-side defence in depth (migration `SecurityAndIndexes`):

- **Row-level security** on `Notification`: `SessionContextInterceptor` sets
  `SESSION_CONTEXT('UserId')` read-only on every connection, and the policy filters to that
  user's rows. No user (login, background jobs, migrations) means no filter.
- **`vw_UserDirectory`**: the staff directory reads a view with no password hash or token columns.

Two migrations: `InitialCreate`, `SecurityAndIndexes`.

## API

10 controllers, all delegating to a service — no logic in the controller, per the architecture
doc's layering: `Api.Controller` → `Api.Services` → `Api.DataAccess`.

`Auth`, `Tasks`, `Projects`, `Requests`, `Meetings`, `Notifications`, `Users`, `Files`,
`Events`, `Reports`, plus `ApiControllerBase`.

## Services

19 services under `Services/`, all registered in `Program.cs`. One use-case handler per use
case; the `DbContext` is the unit of work, one `SaveChanges` per operation.

| Group | Services |
|---|---|
| Use cases | `TaskService`, `TaskAssignmentService`, `ProjectService`, `RequestReviewService`, `MeetingMinutesService`, `EventService`, `UserService`, `StaffOverviewService`, `ReportingService` |
| Auth & access | `AuthService`, `AccessGuard` |
| Files | `FileUploadService`, `FileManagementService`, `BlobStorageService` |
| Notifications | `NotificationService`, `NotificationQueryService`, `EmailService` |
| Cross-cutting | `AuditService`, `MaintenanceJobs` |

`MaintenanceJobs` is the one `IHostedService`: SM-02's 48-hour escalation, SM-01's deadline
reminders, and `Task_Schedule`'s recurring-task generation — the transitions driven by a clock
rather than a user action.

`AccessGuard` is the object-level authorisation check (the anti-IDOR requirement). It is
currently called from `TaskService`, `ProjectService`, `MeetingMinutesService` and
`FileManagementService` only — see the gaps below.

State machines SM-01 (`ProjectTask`), SM-02 (`ResourceRequest`) and SM-03 (`User`) live in
`Data/Lifecycle.cs` as guarded transition tables, checked server-side.

## Clients

Both front ends authenticate against `AuthController` and keep no data of their own: every
list is fetched from the API, and a SignalR push tells them which list to refetch.

### `Frontend_Mobile` — .NET MAUI

Ported from the standalone MAUI app at `C:\3rdYear\XBCAD\Mobile`, which was itself a port of
the Kotlin/Compose Android prototype, itself a port of the Blazor/MudBlazor original. Same
screens, same palette, same role gating at each step.

| Piece | Where |
|---|---|
| Navigation shell, role-gated flyout, unread-count bell, dark-mode toggle | `AppShell.xaml` |
| Screens | `Pages/` — Login, Home, Tasks, CreateTask, Requests, Events, Notifications, ComingSoon |
| Session, API calls, SignalR hub connection (the web client's `AppDataService`, ported) | `Data/AppState.cs` |
| Read-only view models over the `Shared` DTOs, with display-formatting properties (so the XAML needs no converters) | `Models/Models.cs` |
| Palette and styles, light/dark via `AppThemeBinding` | `Resources/Styles/` |

Five things about it are deliberate, not oversights:

- **Icons are Unicode glyphs, not Material Icons.** MAUI doesn't bundle an icon font and
  adding one wasn't worth the dependency. Swap a Material Icons `.ttf` into `Resources/Fonts`
  and change the `Glyph` properties in `Models.cs` for exact parity.
- **Ticking a task completes it as the signed-in user**, as on the web: the API records who.
  The old "completed by" picker is gone. SM-01 has no To Do → Done edge, so a tick sends two
  guarded moves (→ In Progress → Done). The deny reason is a native `DisplayPromptAsync`.
  New Task is a full modal page instead, because
  no MAUI inline dialog fits a form that size.
- **Events are API projects**, as on the web client: coordinator = owner, staff = members.
  Projects have no date, location, budget, capacity or resource list, so those fields were
  dropped from the form rather than kept as inputs that save nowhere.
- **The API address is hard-coded** in `AppState.ApiBase`: `https://10.0.2.2:7272/` on the
  Android emulator (the host's localhost), `https://localhost:7272/` elsewhere. Debug builds
  trust the ASP.NET dev certificate (issuer `CN=localhost`); release builds trust nothing
  extra. A physical phone or a deployed API needs that address changed. The session token is
  kept in `SecureStorage` and resumed on the next launch while it's still valid.
- **Login is a Shell route, not a separate root page.** Swapping the window root between a
  `ContentPage` and a `Shell` crashes Android's fragment manager
  ([dotnet/maui#21257](https://github.com/dotnet/maui/issues/21257)), so the Shell owns the
  whole session and navigates between `//login` and `//home`.

Meetings and User Management are still "Coming soon" stubs behind the right role gating.

Verified building clean (0 warnings, 0 errors) on `net10.0-android` and
`net10.0-windows10.0.19041.0`; the Windows build launches and reaches the login screen.

> **Android gotcha:** changing `<ApplicationId>` leaves the old package's R classes in `obj`,
> and the mismatched resource IDs surface as runtime crashes pointing nowhere near the cause
> ([dotnet/maui#31703](https://github.com/dotnet/maui/issues/31703)). Clear `bin`/`obj` and
> reinstall fresh after any ID change.

### `Frontend_BlazorWeb` — Blazor WebAssembly

Ported from the Blazor Server + MudBlazor prototype; the `@rendermode InteractiveServer`
directives were stripped, since they're meaningless in standalone WASM. `AppDataService.cs`
is the in-memory data layer, carried over from the prototype.

Two accepted consequences of the Server→WASM move, known and temporary:

- A singleton is now per browser tab, not shared server-wide, so the prototype's shared-demo-
  data behaviour across tabs no longer holds.
- The seeded demo users' plaintext passwords ship inside the WASM bundle. Harmless for fake
  data; would not be fine if real credentials ever landed in that file.

## CI/CD

Four workflows in `.github/workflows/`:

| Workflow | Trigger | Does |
|---|---|---|
| `ci.yml` | every branch except deploy branches, + PRs into them | restore, build, test, upload coverage. Holds no credentials. |
| `cd-staging.yml` | push to `develop` / `dev` | build → deploy to staging slot → EF migrations → smoke test |
| `cd-production.yml` | push to `main` / `master` | build → deploy to prod staging slot → migrations → smoke test → **manual-approval swap** |
| `cd-mobile.yml` | changes under `src/Npo.Mobile/**` | Android Release build → signed APK artifact |

Quality gates, in every build job:

- **Zero warnings.** `Directory.Build.props` sets `TreatWarningsAsErrors` for every project,
  so a local build fails the same way CI does.
- **Unit and integration tests.** `ApiIntegrationTests` boots the real API with
  `WebApplicationFactory`, migrates a throwaway SQL Server database from empty, and drops it
  afterwards. Locally that is LocalDB; in CI a `mssql` service container, via `TEST_SQL_CONNECTION`.
- **Coverage threshold.** `.github/scripts/coverage-gate.sh 70` fails the job below 70% line coverage.
- **Smoke test after deploy.** `.github/scripts/smoke-test.sh` checks `/health`, signs in as
  `SMOKE_EMAIL`, and makes one authenticated read, before any slot swap.

The API sends `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` and is rate
limited (300 requests/min per user, 10/min per IP on the anonymous auth routes; both under
`RateLimit:*`). The web client is published in the build job and deployed prebuilt:
`.github/scripts/web-config.sh` points its `appsettings.json` at the API and writes
`staticwebapp.config.json`, whose CSP allows Blazor's inline import map by hash (it changes per build).

**`cd-mobile.yml` is dormant and currently cannot fire.** Its path filter and `MOBILE_PROJECT`
still point at `src/Npo.Mobile/Npo.Mobile.csproj`, a layout this repo never adopted. The MAUI
app lives at `Frontend_Mobile/Frontend_Mobile.csproj`. Its `SHARED_TESTS` path
(`tests/Npo.Shared.Tests/`) doesn't exist either. Repoint all three before expecting an APK.

Deploy jobs are skipped while `vars.AZURE_WEBAPP_NAME` / `AZURE_WEBAPP_NAME_PROD` are unset,
so builds stay green before Azure is provisioned.

Production deploys to a slot and swaps only after approval, configured as required reviewers
on the `production` GitHub environment. Roll back by running the same swap again. Staging
migrations run against the database the live build is still using, so **every migration must
be backward compatible**.

Required repository config before the first real deploy:

| Kind | Name |
|---|---|
| Variables | `AZURE_WEBAPP_NAME`, `AZURE_WEBAPP_NAME_PROD`, `AZURE_RESOURCE_GROUP`, `AZURE_STATIC_WEB_APP*` |
| Secrets | `AZURE_CREDENTIALS`, `STAGING_DB_CONNECTION`, `PRODUCTION_DB_CONNECTION`, `AZURE_STATIC_WEB_APPS_API_TOKEN*`, `SMOKE_EMAIL`, `SMOKE_PASSWORD` (a low-privilege account), `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`, `ANDROID_KEYSTORE_PASSWORD` |
| Azure config | API `Cors:Origins` must list the Static Web Apps URL; the storage account's CORS must allow `PUT`/`GET` from it, since browsers upload and download files straight to Blob Storage. |

Mobile builds unsigned and warns when the keystore secret is absent. Play Store distribution
is deliberately not automated — Phase 1 side-loads the APK.

## What's not built yet

Roughly in dependency order:

1. **Mobile screens for the rest of the API.** The web client now has Files (UC-07), Reports
   (UC-09), Calendar (event attendees and RSVPs), task comments, minutes search/approval,
   request fulfilment, admin announcements and notification deep links. MAUI has none of
   these, its Meetings and User Management are still stubs, and it lacks the offline SQLite cache.
2. **Mobile push** (Notification Hubs) — an NFR, absent. Email via Resend is wired in
   `EmailService` but stays in-app-only with no API key set.
3. **`cd-mobile.yml` paths** — see above; the workflow cannot fire as written.
4. **Localisation and accessibility.** Mobile UI text is inline in XAML rather than `.resx`,
   and `SemanticProperties` are not set anywhere.
