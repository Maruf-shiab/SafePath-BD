# SafePath BD — Coding Guidelines

## 1. Goal

These rules exist so GitHub Copilot/Opus and human developers produce a consistent ASP.NET Core codebase.

> Before modifying code, read all documentation files in `/docs`.

---

# 2. Most Important Rules

1. **Do not change the existing MySQL schema without explicit approval.**
2. **Do not invent table/column names.**
3. **Keep controllers thin.**
4. **Put business rules in services.**
5. **Use DTOs/ViewModels instead of exposing EF entities everywhere.**
6. **Use async database operations.**
7. **Validate all user input.**
8. **Never store or log plain-text passwords.**
9. **Never commit database passwords/API secrets.**
10. **Build and verify after each meaningful implementation phase.**

---

# 3. C# Style

Use standard .NET conventions.

### PascalCase
- classes
- methods
- properties
- public members

### camelCase
- local variables
- parameters

### Interfaces
Prefix with `I`.

Examples:

```text
IRouteService
ISafetyScoreService
IReportService
```

### Async methods

Suffix with `Async`.

Example:

```csharp
Task<RouteComparisonResult> CompareRoutesAsync(...)
```

---

# 4. Nullable Reference Types

Enable nullable reference types if the project template supports it.

Represent database nullability accurately.

Do not silence warnings with `!` everywhere. Fix the model or flow correctly.

---

# 5. Controller Rules

Controllers should:

- receive request,
- check model state,
- call service,
- map result to response/view,
- handle expected result states.

Controllers should not:
- write large LINQ queries,
- calculate safety scores,
- hash passwords manually,
- call routing/weather APIs directly,
- manipulate SQL strings,
- contain 200-line actions.

---

# 6. Service Rules

Services own business behavior.

Example responsibilities:

### `AuthService`
- register
- hash/verify password
- load roles
- create application identity data

### `ReportService`
- shared report queries
- vote/comment behavior
- report status behavior

### `AccidentService`
- create accident report
- create verified accident after review

### `HazardService`
- create hazard report
- active hazard behavior

### `SafetyScoreService`
- normalize factors
- load weights
- calculate score
- save factor breakdown

### `RouteService`
- request candidate routes
- map/match route to road segments
- calculate route safety
- compare routes
- save route

---

# 7. EF Core Rules

- Use one `SafePathDbContext`.
- Use existing schema mappings.
- Use async operations such as `ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`.
- Use `AsNoTracking()` for read-only views.
- Use transactions for multi-table operations requiring atomicity.
- Avoid unnecessary `Include`.
- Select only required fields for large queries.
- Avoid N+1 queries.
- Respect database FK delete behavior.
- Do not automatically run migrations on startup.

---

# 8. Database-First Rule

During initial setup:

- Connect to `safepath_bd`.
- Scaffold or map existing entities.
- Verify table/column names.
- Keep DB-generated triggers/views intact.
- Do not recreate lookup tables in application code unnecessarily.

If the generated EF model is messy, refactor carefully without changing database semantics.

---

# 9. Security

## Passwords
Use a reputable ASP.NET password hashing mechanism.

Never:
- store plain password,
- log password,
- return hash.

## Cookies
- HttpOnly
- Secure in production
- SameSite appropriate for application
- reasonable expiration

## Authorization
Protect actions using server-side role checks.

## CSRF
MVC forms that modify data should use antiforgery protection.

## SQL Injection
Use EF Core parameterized queries. Never build SQL from untrusted user input.

## XSS
Use Razor's default encoding. Avoid rendering raw user HTML.

---

# 10. Input Validation

Use DataAnnotations and/or dedicated validation logic.

Server-side validation is mandatory even if JavaScript validation exists.

Examples:
- coordinate ranges
- positive distances
- valid risk level
- file type/size
- title/description limits
- allowed report transitions

---

# 11. Logging

Use `ILogger<T>`.

Log:
- important workflow failures,
- external provider failures,
- unexpected exceptions,
- admin/security-relevant events.

Do not log:
- passwords,
- password hashes,
- API secrets,
- full connection strings,
- unnecessary precise user location.

---

# 12. Error Handling

Prefer centralized exception handling.

Services may return typed results for expected business failures.

Example:

```text
Success
NotFound
ValidationError
Conflict
Forbidden
```

Do not use exceptions for normal validation flow.

---

# 13. External Integrations

Use `HttpClientFactory`.

Each provider should:
- have an interface,
- have typed configuration,
- handle timeout,
- handle unavailable service,
- map provider response into internal DTOs.

Never let provider-specific response objects spread through the whole application.

---

# 14. Frontend / Razor Rules

- Keep Razor views focused on rendering.
- Put reusable UI into partial views.
- Keep page-specific JS separate when logic becomes non-trivial.
- Do not embed connection strings or secret keys.
- Avoid giant inline scripts.
- Use Bootstrap consistently.
- Make map pages responsive.

---

# 15. JavaScript Rules

For map/API calls:
- use `fetch` or a consistent library,
- check HTTP status,
- handle loading/error state,
- do not assume every request succeeds,
- sanitize/escape user-visible data,
- keep map marker creation in reusable functions.

---

# 16. CSS Rules

Prefer:
- global tokens in site CSS,
- module/page CSS only where needed,
- responsive design,
- accessible contrast,
- consistent spacing.

Do not create dozens of redundant style files at project start.

---

# 17. Naming Guidelines for Project Files

Examples:

```text
Controllers/RouteController.cs
Services/Interfaces/IRouteService.cs
Services/Implementations/RouteService.cs
Models/DTOs/Routes/RouteRequestDto.cs
Models/DTOs/Routes/RouteResultDto.cs
Models/ViewModels/Routes/RoutePageViewModel.cs
```

Avoid ambiguous names like:
- `Helper.cs`
- `Manager.cs`
- `Data.cs`
- `Utils.cs`

unless their responsibility is very clear.

---

# 18. Testing Rules

High-value unit tests:
- safety score calculation
- route comparison
- report verification rules
- vote behavior
- authentication validation

Integration tests:
- database connection
- report creation transaction
- authorization
- critical API endpoints

Do not try to test trivial framework behavior.

---

# 19. Comments and Documentation

Code should be readable without excessive comments.

Comment:
- non-obvious algorithm decisions,
- safety-score formula assumptions,
- provider workarounds,
- privacy/security choices.

Do not comment every line.

---

# 20. Git Rules

Do not commit:
- `bin/`
- `obj/`
- `.vs/`
- private `appsettings.Development.json` if it contains secrets
- `.env`
- user secrets
- uploads
- generated runtime logs

Commit:
- schema SQL file
- sanitized config templates
- docs
- source code
- tests

---

# 21. AI-Specific Working Rules

When GitHub Copilot/Opus receives a task:

1. Read all files in `/docs`.
2. Inspect existing code before creating duplicate classes.
3. Confirm exact DB entity names.
4. State which files will change when useful.
5. Make the smallest coherent change.
6. Do not rewrite unrelated modules.
7. Do not delete working code to simplify a task.
8. Do not add a new NuGet package unless necessary.
9. Do not change architecture without documenting why.
10. After implementation:
    - restore packages if needed,
    - build,
    - run relevant tests,
    - fix all compilation errors.
11. If an external provider/API is not configured, create a clean abstraction and documented configuration placeholder rather than fabricating working credentials.
12. Never pretend a feature is verified if it was not run/tested.
