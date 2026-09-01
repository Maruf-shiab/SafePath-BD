# SafePath BD — Features

## 1. Feature Philosophy

SafePath BD is not just a map and not just an accident-reporting portal. Its key value is the combination of:

- navigation,
- road-safety information,
- community reporting,
- verified accident/hazard data,
- road-segment safety scoring,
- emergency-service discovery,
- and administrative verification.

Features should be implemented in phases. Version 1 must be functional and credible rather than trying to imitate every capability of Google Maps/Waze.

---

# 2. Public / Guest Features

## 2.1 Landing Page

Should explain:
- What SafePath BD does.
- Why safe-route selection matters.
- Main project benefits.
- How community reporting works.
- Call-to-action to open map/register/login.

Suggested sections:
- Hero
- Safe Route
- Hazard Awareness
- Emergency Assistance
- How It Works
- Statistics preview
- Footer

---

## 2.2 Public Map

Depending on privacy and project scope, guests may see:
- verified accident markers,
- verified active hazards,
- emergency services,
- road risk visualization,
- route search.

Unverified/private reports must not be publicly exposed accidentally.

---

## 2.3 Emergency Service Search

Guest can:
- use/select a location,
- view nearest active emergency facilities,
- see name/type/address,
- see phone/emergency phone,
- open map location.

---

# 3. Authentication & User Account

## 3.1 Registration

Required:
- full name
- email
- password
- confirm password

Optional:
- phone

Rules:
- unique email
- secure password hashing
- validation
- default User role assignment

---

## 3.2 Login / Logout

Login with email/password.

After successful login:
- load roles,
- create auth cookie,
- update last login time.

---

## 3.3 Profile

User can:
- view/update allowed profile fields,
- manage profile image later if required,
- see own reports,
- see saved places/routes,
- see notifications.

---

# 4. Interactive Map Module

## 4.1 Base Map

Map should support:
- zoom/pan,
- current location,
- select point,
- start/destination markers.

## 4.2 Map Layers

Potential layers:
- Verified accidents
- Verified hazards
- Emergency services
- Road safety/risk
- Route alternatives

Allow user to toggle layers when UI becomes crowded.

## 4.3 Marker Details

Clicking a marker should show safe public data.

Accident marker:
- type
- severity
- date/time
- location
- status/source as appropriate

Hazard marker:
- hazard type
- risk level
- status
- report time
- community confirmation count

Emergency marker:
- service type
- name
- phone
- opening hours

---

# 5. Safe Route Recommendation

This is the signature feature.

## 5.1 Route Input

User can:
- use current location,
- search/select start,
- search/select destination.

## 5.2 Route Types

System should support:
- SAFEST
- FASTEST
- SHORTEST

## 5.3 Route Result

Each candidate should display:
- distance
- estimated duration
- overall safety score
- route type
- major risk warnings
- map polyline

## 5.4 Safety Explanation

For safest route, show why:

Example:
- Accident risk: Low
- Active hazard risk: Moderate
- Road condition: Good
- Traffic risk: Moderate
- Weather risk: Low
- Overall score: 82/100

Do not present a score as scientific certainty. It is an application risk indicator based on available data.

---

# 6. Accident Reporting

## 6.1 Submit Accident Report

Registered user can submit:
- map location
- optional road segment
- title
- description
- accident type
- severity
- occurred date/time
- number of vehicles if known
- injured count if known
- death count if known
- weather notes
- supporting image(s)

Initial status:
`PENDING`

## 6.2 Accident Report Details

Show:
- public report data
- status
- image(s)
- comments
- confirm/dispute counts
- verification result if available

## 6.3 Verified Accident Conversion

Admin/Moderator may verify a community accident report.

A verified/historical accident can be stored in `accidents` with `source_report_id`.

This trusted record may influence long-term road safety.

---

# 7. Hazard Reporting

Registered user can report:

- Pothole
- Broken Road
- Waterlogging
- Broken Traffic Signal
- Road Construction
- Illegal Parking
- Poor Street Lighting
- Fallen Tree
- Road Block
- Dangerous Intersection
- Debris on Road
- Other

Data:
- location
- optional road segment
- title
- description
- hazard type
- risk level
- observed time
- optional expected clearance
- image(s)

Initial status:
`PENDING`

---

# 8. Community Verification Features

## 8.1 Confirm / Dispute

User can:
- CONFIRM a report
- DISPUTE a report

One effective vote per user/report.

The system setting `report_vote_confirmation_threshold` may help prioritize reports but should not automatically make a report officially verified.

## 8.2 Comments

Users can:
- comment,
- reply to comments,
- view discussion.

Support soft-deleted comments.

## 8.3 Verification

Moderator/Admin can:
- place report under review,
- verify,
- reject,
- mark duplicate,
- request more information,
- resolve.

Every decision should be logged in `report_verifications`.

---

# 9. Road & Road Segment Management

Admin should be able to manage:
- roads
- road segments
- start/end locations
- distance
- speed limit
- lanes
- one-way flag
- geometry/polyline
- active status

This data is essential for safety scoring.

---

# 10. Road Condition Module

Admin or trusted source can record:
- surface condition
- surface score
- lighting score
- drainage score
- visibility score
- overall condition score
- description
- source type

Historical entries should be preserved.

---

# 11. Traffic Module

Store:
- traffic level
- average speed
- congestion score
- source
- timestamp

Version 1 can use manually seeded/sample data or an external provider if available.

The application must not falsely claim real-time traffic if the source is not actually real-time.

---

# 12. Weather Module

Store:
- weather type
- temperature
- rainfall
- visibility
- weather risk score
- source
- timestamp

Weather integration can be optional in early phases.

---

# 13. Safety Score Engine

## 13.1 Inputs

- Verified historical accident risk
- Active verified hazard risk
- Road condition
- Traffic
- Weather
- Lighting

## 13.2 Output

For each road segment:
- 0–100 overall safety score
- risk level
- methodology version
- factor breakdown

Suggested display bands:

| Safety Score | Display Meaning |
|---:|---|
| 81–100 | Very Safe / Low Risk |
| 61–80 | Generally Safe / Moderate-Low Risk |
| 41–60 | Caution / Moderate Risk |
| 21–40 | Risky / High Risk |
| 0–20 | Very Risky / Critical |

These display bands are application conventions, not legal or scientific classifications.

## 13.3 Recalculation

Possible triggers:
- new verified accident
- hazard verified/resolved
- new road condition
- new traffic/weather data
- scheduled recalculation
- on-demand route calculation if cached score is stale

---

# 14. Accident Hotspot / Risk Visualization

The map can highlight:
- segments with repeated verified accidents,
- high-risk segments,
- critical active hazards.

Version 1 may use simple counts and safety scores.

Advanced clustering/prediction can be future work.

---

# 15. Emergency Assistance

Features:
- current/map-selected location
- nearby emergency facilities
- filter by type
- phone contact
- 24-hour indicator
- verified facility indicator

Types:
- Hospital
- Police Station
- Fire Service
- Ambulance
- Emergency Center

---

# 16. Saved Places

Registered user can save:
- Home
- Office
- University
- Favorite
- Other

Used to speed up route selection.

---

# 17. Saved Routes

User can:
- save a generated route,
- give it a custom name,
- reopen it later.

If route data is stale, UI should make clear that current risk/traffic may differ and optionally recalculate.

---

# 18. Notifications

Initial in-app notifications:

- hazard alert
- accident alert
- emergency alert
- report verified
- report rejected
- report resolved
- road risk alert
- system message

Features:
- notification list
- unread count
- mark read
- optional link to related report/route/road segment

---

# 19. Admin Dashboard

Dashboard may show:
- total users
- pending reports
- verified reports
- active hazards
- verified accidents
- high-risk road segments
- emergency facilities
- recent admin actions

Admin pages:
- Users
- Reports
- Roads
- Segments
- Accidents
- Emergency Services
- Feedback
- Settings
- Audit Log

---

# 20. Feedback

User can submit:
- subject
- message
- optional rating

Admin can move feedback through:
- OPEN
- IN_REVIEW
- RESOLVED
- CLOSED

---

# 21. Analytics

Academic Version 1 analytics can include:
- reports by type/status
- accidents by severity/type
- hazards by type
- most reported areas/segments
- highest-risk road segments
- report verification counts
- report trends over time

Do not make unsupported causal claims.

---

# 22. Version 1 Priorities

## Must Have

- Database connection
- Authentication/roles
- Map
- Accident report
- Hazard report
- Admin verification
- Road/segment model
- Safety score service
- Route comparison
- Emergency services
- Basic notifications

## Should Have

- Saved places
- Saved routes
- Votes
- Comments
- Images
- Dashboard analytics

## Future

- Mobile app
- Push notifications
- Government/open-data import
- Real-time traffic integration
- Machine learning risk prediction
- Automatic incident detection
- Live emergency dispatch integration
- Advanced map matching
