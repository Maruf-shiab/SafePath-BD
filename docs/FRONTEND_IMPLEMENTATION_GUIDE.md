# SafePath BD — Frontend Implementation Guide

## 1. Goal

Build a highly polished ASP.NET Core MVC frontend without turning the project into a fragile animation demo.

Use:
- Razor
- Bootstrap selectively
- custom CSS
- JavaScript modules
- Leaflet
- GSAP only where motion complexity justifies it

---

## 2. Recommended Frontend Structure

```text
SafePathBD.Web/
├── Views/
│   ├── Shared/
│   │   ├── _Layout.cshtml
│   │   ├── _Navbar.cshtml
│   │   ├── _Footer.cshtml
│   │   ├── _Toast.cshtml
│   │   └── _LoadingOverlay.cshtml
│   │
│   ├── Home/
│   ├── Map/
│   ├── Reports/
│   ├── Emergency/
│   └── Profile/
│
├── wwwroot/
│   ├── css/
│   │   ├── tokens.css
│   │   ├── base.css
│   │   ├── layout.css
│   │   ├── components.css
│   │   ├── motion.css
│   │   ├── map.css
│   │   └── responsive.css
│   │
│   ├── js/
│   │   ├── app.js
│   │   ├── motion.js
│   │   ├── map.js
│   │   ├── route-ui.js
│   │   ├── report-ui.js
│   │   └── notifications.js
│   │
│   └── images/
```

Do not create every file immediately. Create when needed.

---

## 3. CSS Strategy

Use:
- CSS variables for tokens
- small reusable utility classes
- page-specific CSS only when necessary

Avoid:
- giant 5000-line site.css
- inline style everywhere
- repeated hardcoded colors
- overriding Bootstrap randomly

---

## 4. Bootstrap Usage

Bootstrap is allowed for:
- grid
- spacing helpers
- basic responsive behavior

But SafePath BD should not look like default Bootstrap.

Customize:
- buttons
- cards
- navbar
- forms
- modal
- badges

---

## 5. Animation Strategy

### Use CSS transitions for:
- hover
- focus
- card interaction
- status change
- simple panels

### Use JavaScript / GSAP for:
- hero animation
- route line reveal
- advanced scroll sequences
- coordinated map panel motion

Avoid using GSAP for every button.

---

## 6. Map UI

Map should be treated as an application workspace.

Desktop layout example:

```text
┌──────────────────────────────────────────────┐
│ Navbar                                       │
├───────────────┬──────────────────────────────┤
│ Search/Route  │                              │
│ Controls      │         MAP                  │
│               │                              │
│ Route Cards   │                              │
│               │                              │
└───────────────┴──────────────────────────────┘
```

Alternative:
- full map
- floating panels

Prefer the second for a premium feel.

---

## 7. Motion Entry Sequence

For major pages:

1. page background visible instantly
2. primary heading fades/slides
3. main controls appear
4. secondary content follows
5. avoid long blocking intro

Total page entry should feel fast.

---

## 8. Loading Experience

Never leave blank content while waiting.

Examples:
- route card skeleton
- map shimmer
- subtle loading badge
- button spinner with fixed width

---

## 9. Error Experience

Errors should feel integrated.

Example:
“Couldn’t load nearby emergency services. Try again.”

Provide:
- short reason
- retry action

Do not show raw exception messages.

---

## 10. Map Error Handling

Examples:
- location permission denied
- geocoding failed
- route provider unavailable
- no route returned

Each should have a dedicated friendly UI state.

---

## 11. Performance

Critical:
- lazy-load images
- compress report images
- avoid heavy animation on every list item
- defer noncritical JS
- minimize layout thrashing
- animate transform/opacity
- limit map markers using bounds/clustering

---

## 12. Mobile

Mobile should use:
- bottom sheets
- sticky action buttons
- full-width map
- simplified side panels
- large tap targets

Avoid:
- desktop sidebar squeezed into 360px width
- tiny map controls

---

## 13. AI Implementation Workflow

For each page:

1. Read UI docs.
2. Identify primary user task.
3. Sketch hierarchy before coding.
4. Reuse design tokens/components.
5. Implement desktop.
6. Implement mobile.
7. Add motion.
8. Add loading/error/empty states.
9. Check reduced motion.
10. Check that page does not look like generic template UI.

---

## 14. Quality Gate

Before considering a frontend page complete:

- no default Bootstrap visual feel
- no random gradients
- no inconsistent radii
- no duplicate button styles
- no unhandled loading state
- no unhandled error state
- no inaccessible contrast
- no motion that blocks interaction
- responsive at common widths
- map UI remains usable
