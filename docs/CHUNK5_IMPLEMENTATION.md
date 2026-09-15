# SafePath BD — Chunk 5 Implementation Map

This file documents where the Chunk 5 implementation lives. It does **not** change the database schema.

## New backend files

```text
SafePathBD.Web/
├── Common/
│   └── NotificationConstants.cs
├── Models/
│   ├── DTOs/
│   │   ├── Dashboard/DashboardDtos.cs
│   │   └── Notifications/NotificationDtos.cs
│   └── ViewModels/Notifications/NotificationPageViewModel.cs
├── Services/
│   ├── Interfaces/
│   │   ├── IDashboardService.cs
│   │   └── INotificationService.cs
│   └── Implementations/
│       ├── DashboardService.cs
│       └── NotificationService.cs
├── Controllers/
│   └── NotificationsController.cs
├── ViewComponents/
│   └── NotificationBellViewComponent.cs
└── Areas/Admin/Controllers/
    └── ModeratorDashboardController.cs
```

### NotificationService

Uses the existing `notification_types` and `notifications` tables. It provides paginated/current-user reads, unread count, mark-one-read, mark-all-read and report-workflow notification creation. IDs are resolved from stable `type_code` values rather than hard-coded lookup IDs.

The actual database supplies `REPORT_VERIFIED`, `REPORT_REJECTED`, `REPORT_RESOLVED` and `SYSTEM`. `NEEDS_INFO` and `DUPLICATE` therefore use `SYSTEM` without inserting lookup rows.

### Moderation integration

`ReportModerationService` remains the sole status-transition owner. It queues the reporter notification before its existing `SaveChangesAsync` inside the moderation transaction. Invalid/stale repeated transitions do not produce a second successful notification.

### DashboardService

Provides three read-only projections:

- `GetUserDashboardAsync(userId)` — current user's counts/reports/action-required/notifications/activity only.
- `GetModeratorDashboardAsync()` — real moderation totals, open queue preview and verification history.
- `GetAdminDashboardAsync()` — real user/report totals, open queue preview and `admin_actions`.

## New/updated web UI

```text
SafePathBD.Web/
├── Views/
│   ├── Notifications/Index.cshtml
│   ├── Dashboard/Index.cshtml                 (updated)
│   ├── Home/StatusCode.cshtml
│   └── Shared/
│       └── Components/NotificationBell/Default.cshtml
├── Areas/Admin/Views/
│   ├── Dashboard/Index.cshtml                 (updated)
│   └── ModeratorDashboard/Index.cshtml
└── wwwroot/
    ├── css/
    │   ├── dashboard.css
    │   └── notifications.css
    └── js/
        ├── dashboard.js
        └── notifications.js
```

The shared navbar/layout were updated to integrate Notifications, role-correct dashboard links, active states and the bell. Mobile navigation collapses before the denser authenticated links overflow.

## Notification endpoints

All endpoints are authenticated; no `userId` is accepted as browser authority.

```text
GET  /Notifications
GET  /api/v1/notifications/latest?take=6
GET  /api/v1/notifications/unread-count
POST /api/v1/notifications/{id}/read
POST /api/v1/notifications/read-all
POST /Notifications/{id}/read
POST /Notifications/read-all
```

State-changing endpoints use antiforgery protection. Notification ownership is enforced in the query/update predicate.

## Workflow notification mapping

| Report status | Existing notification type | Reporter message behavior |
|---|---|---|
| VERIFIED | REPORT_VERIFIED | Outcome notification |
| REJECTED | REPORT_REJECTED | Outcome only; internal review note not exposed |
| NEEDS_INFO | SYSTEM | Includes the reporter-facing information request when supplied |
| DUPLICATE | SYSTEM | Outcome only; internal review note not exposed |
| RESOLVED | REPORT_RESOLVED | Outcome notification |
| UNDER_REVIEW | none | Intentionally suppressed to avoid noise |

## Tests added

```text
SafePathBD.Tests/
├── NotificationTests.cs
└── DashboardServiceTests.cs
```

`ReportTestContext.cs` was extended with the real seeded notification type codes and the new services.

The tests cover workflow notification generation, controlled `SYSTEM` fallback, deduplication through authoritative status transitions, ownership, unread/read behavior, user dashboard isolation, needs-information surfacing, moderator operational data and admin real counts.

## Files intentionally not changed

- `database/SafePath_BD_Full_Database_MySQL.sql`
- Generated database entities / `SafePathDbContext` schema mapping
- Routing integration placeholder
- Safety-score implementation
- Saved places/routes implementation

No migration, `EnsureCreated`, schema DDL or automatic lookup seeding was added.

## Local verification commands

Run from the repository root on a machine with .NET 8 SDK installed:

```powershell
dotnet restore
dotnet build
dotnet test
```

Then run the application and manually verify the reporter → moderator → notification → dashboard scenario described in the Chunk 5 requirements.
