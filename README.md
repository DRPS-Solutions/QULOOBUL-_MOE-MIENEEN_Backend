# QULOOBUL _MOE'MIENEEN_Backend


ASP.NET Core Web API for the Quloobul Moe'mieneen NPO platform — projects, tasks,
resource requests, meetings and file storage for one organisation's staff and volunteers.
Serves both clients: a Blazor web app and a .NET MAUI Android app.

**Status: scaffolding + data model.** The entity classes are written. Controllers, the
DbContext and the service bodies are not. See [What's not built yet](#whats-not-built-yet)
before you assume an endpoint exists.

## Stack

| | |
|---|---|
| Runtime | .NET 10 (`net10.0`), nullable + implicit usings on |
| Data | EF Core 10, SQL Server provider |
| Files | Azure Blob Storage — Azurite for local development |
| API docs | OpenAPI (`Microsoft.AspNetCore.OpenApi`), dev environment only |
| Container | Linux, `mcr.microsoft.com/dotnet/aspnet:10.0` |
| CI/CD | GitHub Actions → Azure App Service (slot swap) + Static Web Apps |

## Running it

```bash
dotnet run --project "QULOOBUL _MOE'MIENEEN_Backend/QULOOBUL _MOE'MIENEEN_Backend.csproj"
```

`http://localhost:5012`, or `https://localhost:7272` on the `https` profile. OpenAPI is
mapped at `/openapi/v1.json` in Development only. The project name contains a space and
an apostrophe — quote every path you pass to `dotnet`.

Build the whole solution:

```bash
dotnet build "QULOOBUL _MOE'MIENEEN_Backend.slnx" -c Release -warnaserror
```

CI builds with `-warnaserror`, so a warning locally is a red build remotely.

## Data model

24 entities under `Models/`, one file each, grouped by bounded context. Table names are
set explicitly with `[Table]` to match the ERD (`Task`, `Task_Assignment`, `File_Upload`…),
so the C# names and the SQL names differ on purpose.

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

## Services

`Services/` holds five empty classes marking the seams: `AuditService`,
`BlobStorageService`, `FileUploadService`, `RequestReviewService`, `TaskAssignmentService`.
Names and namespaces only — no implementations, and none are registered in DI.

## CI/CD

Four workflows in `.github/workflows/`:

| Workflow | Trigger | Does |
|---|---|---|
| `ci.yml` | every branch except deploy branches, + PRs into them | restore, build, test, upload coverage. Holds no credentials. |
| `cd-staging.yml` | push to `develop` / `dev` | build → deploy to staging slot → EF migrations → smoke test |
| `cd-production.yml` | push to `main` / `master` | build → deploy to prod staging slot → migrations → smoke test → **manual-approval swap** |
| `cd-mobile.yml` | changes under `src/Npo.Mobile/**` | Android Release build → signed APK artifact |

Two gates are inactive and say so in the log rather than failing:

- **Tests** — every workflow greps for `Microsoft.NET.Test.Sdk` and warns-and-passes when
  no test project exists. There isn't one yet, so the test gate currently proves nothing.
- **Deploy jobs** — skipped while `vars.AZURE_WEBAPP_NAME` / `AZURE_WEBAPP_NAME_PROD` are
  unset, so builds are green before Azure is provisioned.

Production deploys to a slot and swaps only after approval, configured as required
reviewers on the `production` GitHub environment. Roll back by running the same swap again.
Staging migrations run against the database the live build is still using, so **every
migration must be backward compatible**.

Required repository config before the first real deploy:

| Kind | Name |
|---|---|
| Variables | `AZURE_WEBAPP_NAME`, `AZURE_WEBAPP_NAME_PROD`, `AZURE_RESOURCE_GROUP`, `AZURE_STATIC_WEB_APP*` |
| Secrets | `AZURE_CREDENTIALS`, `STAGING_DB_CONNECTION`, `PRODUCTION_DB_CONNECTION`, `AZURE_STATIC_WEB_APPS_API_TOKEN*`, `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`, `ANDROID_KEYSTORE_PASSWORD` |

Mobile builds unsigned and warns when the keystore secret is absent. Play Store
distribution is deliberately not automated — Phase 1 side-loads the APK.

## What's not built yet

Roughly in dependency order:

1. **DbContext** — EF Core packages are referenced but no `DbSet`s, no `OnModelCreating`,
   nothing registered in `Program.cs`.
2. **Connection string** — workflows pass `ConnectionStrings__Default`; `appsettings.json`
   doesn't define it and nothing reads it.
3. **Migrations** — none. Both CD workflows run `dotnet ef database update` regardless.
4. **`/health`** — both CD workflows smoke-test it. `Program.cs` doesn't map it, so the
   first real deploy fails at the smoke test.
5. **Controllers** — `MapControllers()` is called; there are no controllers.
6. **Authentication** — `UseAuthorization()` with no authentication scheme registered.
   `User.PasswordHash` exists; nothing writes or checks it.
7. **Service bodies** — the five stubs above.
8. **Tests** — no test project, which is why the CI test gate is inert.
