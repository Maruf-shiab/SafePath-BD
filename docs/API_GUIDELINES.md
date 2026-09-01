# SafePath BD — API Guidelines

## 1. Purpose

SafePath BD is primarily an ASP.NET Core MVC application, but map interactions and dynamic features will require JSON endpoints.

These endpoints should be consistent, secure, and easy for the JavaScript frontend to use.

Use a predictable `/api/v1/...` convention for JSON APIs.

---

## 2. General API Rules

- Use HTTPS in deployed environments.
- Use JSON for AJAX/API request and response bodies.
- Do not expose EF entities directly.
- Use request/response DTOs.
- Validate all inputs server-side.
- Apply authorization at controller/action level.
- Return correct HTTP status codes.
- Never return passwords, password hashes, database credentials, API secrets, or unnecessary personal data.
- Use UTC or a consistent server time strategy internally and format appropriately for users.
- Log unexpected failures without leaking sensitive details to responses.

---

## 3. Suggested Response Shape

For simple successful responses:

```json
{
  "success": true,
  "message": "Route calculated successfully.",
  "data": {}
}
```

For validation failure:

```json
{
  "success": false,
  "message": "Validation failed.",
  "errors": {
    "destination": [
      "Destination is required."
    ]
  }
}
```

Do not force this envelope on endpoints where standard ASP.NET validation/problem-details is clearer. Consistency is more important than ceremony.

---

## 4. HTTP Methods

Use:

- `GET` — read
- `POST` — create/action with body
- `PUT` — complete update when appropriate
- `PATCH` — partial update when useful
- `DELETE` — delete only where the project explicitly supports it

Examples:

```text
GET    /api/v1/reports/123
POST   /api/v1/reports/accidents
POST   /api/v1/reports/hazards
POST   /api/v1/reports/123/votes
POST   /api/v1/routes/compare
GET    /api/v1/emergency-services/nearby
```

---

## 5. HTTP Status Codes

Use meaningful codes:

- `200 OK` — successful read/action
- `201 Created` — created resource
- `204 No Content` — successful update/delete with no body
- `400 Bad Request` — invalid input
- `401 Unauthorized` — not authenticated
- `403 Forbidden` — authenticated but not allowed
- `404 Not Found` — resource missing
- `409 Conflict` — duplicate/invalid state conflict
- `422 Unprocessable Entity` — optional for business validation
- `500 Internal Server Error` — unexpected server failure

---

## 6. Authentication & Authorization

Use ASP.NET Core cookie authentication for the website.

JSON API endpoints used by the same site may rely on the authenticated cookie.

Roles:
- User
- Moderator
- Admin

Examples:

```text
Public map reads        → Anonymous allowed if configured
Submit report           → User+
Vote/comment            → User+
Verify report           → Moderator/Admin
Manage settings         → Admin only
Manage roles/users      → Admin only
```

Do not rely only on hidden buttons in the UI. Enforce authorization server-side.

---

# 7. Suggested Endpoint Groups

## 7.1 Authentication

MVC routes may be enough:

```text
GET/POST /Account/Register
GET/POST /Account/Login
POST     /Account/Logout
GET      /Account/Profile
```

If JSON auth is later added, do not duplicate logic; reuse `IAuthService`.

---

## 7.2 Locations / Geocoding

Suggested endpoints:

```text
GET /api/v1/locations/search?q=...
GET /api/v1/locations/reverse?lat=...&lng=...
```

Response may include:
- latitude
- longitude
- display address
- landmark
- provider
- external place id

Validate latitude:
- -90 to 90

Validate longitude:
- -180 to 180

---

## 7.3 Map Data

```text
GET /api/v1/map/reports?minLat=&minLng=&maxLat=&maxLng=
GET /api/v1/map/emergency-services?minLat=&minLng=&maxLat=&maxLng=
GET /api/v1/map/road-risk?minLat=&minLng=&maxLat=&maxLng=
```

Use map bounding box parameters to avoid returning every record.

Return only public/verified information where required.

---

## 7.4 Route API

### Compare Routes

```text
POST /api/v1/routes/compare
```

Example request:

```json
{
  "start": {
    "latitude": 23.7800,
    "longitude": 90.4000
  },
  "destination": {
    "latitude": 23.7500,
    "longitude": 90.3700
  }
}
```

Possible response:

```json
{
  "success": true,
  "data": {
    "recommendedRouteType": "SAFEST",
    "routes": [
      {
        "routeId": 101,
        "routeType": "SAFEST",
        "distanceKm": 8.4,
        "durationMinutes": 27,
        "safetyScore": 84.2,
        "warnings": []
      }
    ]
  }
}
```

Do not guarantee three distinct routes if the routing provider returns fewer alternatives.

---

## 7.5 Reports

### Accident

```text
POST /api/v1/reports/accidents
```

### Hazard

```text
POST /api/v1/reports/hazards
```

### Details

```text
GET /api/v1/reports/{id}
```

### User's own reports

```text
GET /api/v1/reports/mine?page=1&pageSize=20
```

Report submission should use a transaction when creating:
- `reports`
- subtype row
- optional images/location references

---

## 7.6 Report Voting

```text
POST /api/v1/reports/{id}/votes
```

Request:

```json
{
  "voteType": "CONFIRM"
}
```

If the user already voted, update or return conflict according to service design. Keep behavior consistent with the DB unique constraint.

---

## 7.7 Comments

```text
GET  /api/v1/reports/{id}/comments
POST /api/v1/reports/{id}/comments
```

Optional reply:

```json
{
  "commentText": "The pothole is still there.",
  "parentCommentId": 42
}
```

---

## 7.8 Report Verification

Moderator/Admin:

```text
POST /api/v1/admin/reports/{id}/verification
```

Request:

```json
{
  "statusCode": "VERIFIED",
  "comment": "Location and image verified."
}
```

Service should:
1. authorize reviewer,
2. validate allowed state transition,
3. insert verification history,
4. update current report status,
5. create notification for reporter,
6. optionally trigger safety-score refresh,
7. log admin action.

---

## 7.9 Emergency Services

```text
GET /api/v1/emergency-services/nearby?lat=&lng=&type=&limit=
```

Return:
- name
- type
- latitude
- longitude
- phone
- emergency phone
- hours
- distance estimate

Do not claim route travel time unless calculated by routing provider.

---

## 7.10 Notifications

```text
GET  /api/v1/notifications
POST /api/v1/notifications/{id}/read
POST /api/v1/notifications/read-all
```

Only return notifications belonging to the authenticated user.

---

# 8. Validation Rules

Validate DTOs before service execution.

Examples:

### Registration
- valid email
- password policy
- name required

### Coordinates
- valid ranges

### Report
- title required
- valid type
- valid location
- hazard/accident subtype fields consistent

### Counts
- injured/deaths/vehicle count cannot be negative

### Safety values
- normalized scores should remain 0–100
- weights should remain valid

---

# 9. File Upload Rules

For report images:

- Allow only expected image MIME types.
- Enforce maximum file size.
- Generate server-side safe filenames.
- Never trust client filename.
- Store outside executable directories when practical.
- Store only URL/path metadata in `report_images`.
- Reject executable/script content.
- Consider image re-encoding in a later hardening phase.

---

# 10. Pagination

Use pagination for:
- reports
- admin users
- feedback
- notifications
- audit log
- accident history

Suggested:

```text
?page=1&pageSize=20
```

Set a reasonable maximum page size, e.g. 100.

---

# 11. Sorting and Filtering

Use explicit allowlists.

Example report filters:
- report type
- status
- date range
- hazard type
- accident severity
- road segment

Do not dynamically concatenate arbitrary SQL order/filter strings.

---

# 12. External API Rules

Every third-party provider must be behind an abstraction.

Examples:

```text
IGeocodingProvider
IRoutingProvider
IWeatherProvider
ITrafficProvider
```

Rules:
- use `HttpClientFactory`
- timeout requests
- handle rate limits
- handle provider failures
- do not expose API keys to browser unless explicitly designed as a public browser key
- cache only where appropriate
- log provider errors without leaking secrets

---

# 13. Privacy Rules

Location data is sensitive.

- Do not expose another user's location history.
- Do not provide endpoints that return `user_location_history` publicly.
- Capture location only for an explicit user action.
- Public report maps should show report location, not private location history.
- Consider precision reduction in future if privacy requires it.

---

# 14. Error Contract

For unexpected errors:
- log correlation/request information server-side,
- return generic message,
- do not return stack trace in production.

Example:

```json
{
  "success": false,
  "message": "An unexpected error occurred."
}
```

---

# 15. API Implementation Rule for AI Assistants

When adding an endpoint:

1. Confirm feature exists in `FEATURES.md`.
2. Confirm tables/fields in `DATABASE_SCHEMA.md`.
3. Create DTO.
4. Validate input.
5. Call a service.
6. Apply authorization.
7. Return correct status.
8. Add tests for important business behavior.
9. Do not place direct external API logic in the controller.
