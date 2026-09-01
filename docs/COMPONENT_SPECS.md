# SafePath BD — Component Specifications

## 1. Purpose

This file defines reusable UI components so the interface remains consistent and does not become a collection of one-off pages.

Components may be implemented as:
- Razor partials
- ViewComponents
- reusable CSS classes
- small JavaScript modules

Do not over-componentize simple markup.

---

# 2. Global Components

## 2.1 Main Navbar

Contains:
- logo
- map
- reports
- emergency
- notifications
- profile/login

Behavior:
- transparent/low-profile over hero
- becomes solid/elevated on scroll
- active item uses accent underline/glow

Mobile:
- compact menu
- clear icon labels

---

## 2.2 Page Header

Contains:
- eyebrow/meta text
- H1
- short description
- optional action button

Use consistently across non-map pages.

---

## 2.3 Primary Button

States:
- default
- hover
- active
- loading
- disabled

No layout shift when loading.

---

## 2.4 Status Chip

Variants:
- safe
- caution
- danger
- pending
- verified
- rejected
- resolved

Include text + icon/dot.

---

# 3. Map Components

## 3.1 Floating Search Panel

Contains:
- start location
- destination
- swap button
- search/compare button

Desktop:
- top-left floating panel

Mobile:
- top compact panel or bottom sheet

---

## 3.2 Route Option Card

Shows:
- route type
- distance
- ETA
- safety score
- risk badge
- optional warning count

Interaction:
- hover raises card
- click activates route
- selected state stronger border/glow

---

## 3.3 Safety Score Ring

Shows:
- score 0–100
- risk label
- animated arc

Must support:
- safe
- moderate
- high risk

---

## 3.4 Risk Factor Row

Example:
Accident Risk — 18%
Hazard Risk — 10%

Contains:
- label
- score
- micro progress bar
- tooltip/help text

---

## 3.5 Map Layer Toggle

Options:
- accidents
- hazards
- emergency
- risk
- traffic (future)

Use compact floating controls.

---

## 3.6 Report Marker

Custom marker styling.

Hover/click:
- marker enlarges slightly
- popup or side panel opens

---

## 3.7 Current Location Marker

Use:
- small center dot
- soft pulse ring
- high contrast

Do not use default Leaflet marker.

---

# 4. Report Components

## 4.1 Report Card

Contains:
- type icon
- title
- area/location
- status
- severity/risk
- time
- confirmation count
- thumbnail if available

Hover:
- subtle lift
- image scale 1.02
- accent edge appears

---

## 4.2 Report Detail Panel

Contains:
- report metadata
- map mini-view
- images
- status
- comments
- confirm/dispute
- verification history if authorized

---

## 4.3 Verification Action Bar

Admin/Moderator only.

Actions:
- verify
- reject
- duplicate
- needs info
- resolve

Use destructive styling carefully.

---

# 5. Emergency Components

## 5.1 Emergency Service Card

Shows:
- type
- name
- distance
- phone
- open/24h status
- verification status

CTA:
- View on Map
- Call (mobile)
- Details

---

# 6. Dashboard Components

## 6.1 Metric Card

Use only meaningful metrics.

Examples:
- Pending Reports
- Verified Hazards
- High-Risk Segments
- Active Alerts

Animation:
- count-up on first load only

---

## 6.2 Activity Timeline

For:
- report submission
- verification
- resolution
- admin actions

---

# 7. Notification Components

## 7.1 Notification Item

Contains:
- type icon
- title
- short message
- time
- read/unread state

Unread:
- subtle accent background

---

# 8. Modal / Drawer Rules

Prefer drawers for map-related details.

Prefer modals for:
- confirmation
- short forms
- focused actions

Avoid using modals for full pages.

---

# 9. Empty State Component

Contains:
- minimal illustration/icon
- title
- explanation
- next action

No generic “No data found.”

---

# 10. Skeleton Loaders

Create reusable skeleton patterns for:
- cards
- report lists
- route cards
- map side panel

Skeleton should match final layout shape.

---

# 11. Component Quality Rule

A component is complete only if it has:
- default state
- hover/focus state
- loading state where needed
- empty/error state where needed
- mobile behavior
- reduced-motion behavior
