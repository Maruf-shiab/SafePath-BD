# SafePath BD — Design System

## 1. Purpose

This file defines the visual system for SafePath BD so all frontend pages remain consistent.

Use this as the source of truth for:
- colors
- typography
- spacing
- radius
- shadows
- surfaces
- buttons
- cards
- status chips
- map UI
- interaction states

Do not hardcode random values across many files.

---

## 2. Design Tokens

Use CSS custom properties.

Example token structure:

```css
:root {
  --bg-primary: #0b1220;
  --bg-secondary: #111827;
  --surface-1: #151d2d;
  --surface-2: #1c2638;

  --text-primary: #f8fafc;
  --text-secondary: #aab4c3;
  --text-muted: #7f8a9a;

  --accent-primary: #35d0ba;
  --accent-primary-soft: rgba(53, 208, 186, 0.14);

  --safe: #3ddc97;
  --caution: #f6c453;
  --warning: #f59e0b;
  --danger: #ef5b5b;

  --border-subtle: rgba(255,255,255,0.08);
  --border-strong: rgba(255,255,255,0.14);

  --shadow-sm: 0 8px 24px rgba(0,0,0,0.18);
  --shadow-md: 0 16px 40px rgba(0,0,0,0.25);
  --shadow-lg: 0 28px 70px rgba(0,0,0,0.35);

  --radius-sm: 10px;
  --radius-md: 16px;
  --radius-lg: 24px;

  --space-1: 4px;
  --space-2: 8px;
  --space-3: 12px;
  --space-4: 16px;
  --space-5: 24px;
  --space-6: 32px;
  --space-7: 48px;
  --space-8: 64px;
}
```

These values may be adjusted globally, but do not create page-specific competing systems.

---

## 3. Color Usage

### Primary Accent
Use for:
- main CTA
- selected route
- selected map tool
- active navigation
- interactive highlights

### Safe
Use for:
- very safe route
- verified success
- resolved state
- positive health indicators

### Caution
Use for:
- medium risk
- needs attention
- temporary state

### Danger
Use only for:
- severe accidents
- critical hazards
- rejected destructive actions
- high-risk route warnings

Do not overuse red.

---

## 4. Typography

Use a modern sans-serif stack.

Recommended:
- Inter
- Manrope
- Plus Jakarta Sans
- system fallback

Hierarchy:

### Display
Hero titles only.

### H1
Main page title.

### H2
Section title.

### H3
Card/module title.

### Body
Primary readable copy.

### Meta
Time, location, category, helper text.

Typography should feel calm and editorial.

Avoid:
- all caps for large blocks
- tiny gray text
- giant bold headings everywhere

---

## 5. Spacing

Use an 8px-based rhythm.

Avoid arbitrary values like:
- 13px
- 27px
- 41px

unless visually necessary.

Typical:
- Card padding: 20–24px
- Section gap: 48–80px
- Form field gap: 16px
- Button horizontal padding: 18–24px

---

## 6. Border Radius

Use radius intentionally.

Suggested:
- inputs: 12px
- buttons: 12–14px
- cards: 16–20px
- large floating panels: 22–26px

Do not make every object pill-shaped.

Pills should mainly be:
- status tags
- filters
- compact route selectors

---

## 7. Surfaces

Use 3 main surface levels.

### Surface A
Main page background.

### Surface B
Primary card/panel.

### Surface C
Elevated floating controls.

Use blur/glass only for floating map controls or overlays.

---

## 8. Buttons

### Primary
High-emphasis action.

### Secondary
Outline or muted surface.

### Ghost
Low-priority.

### Destructive
Delete/reject.

Interaction:
- slight lift
- subtle shadow increase
- quick scale 1.01–1.03
- no excessive bounce

---

## 9. Inputs

Inputs should:
- have strong focus state
- include icons only when useful
- animate border/focus gently
- show validation message immediately but calmly

Map search inputs can use a floating elevated style.

---

## 10. Cards

Cards must have purpose.

Good examples:
- report summary
- safety factor
- emergency service
- route alternative
- analytics summary

Avoid:
- meaningless “four cards in a row” layout
- decorative cards with no interaction

---

## 11. Status Chips

Examples:

Verified
Pending
Rejected
Resolved
High Risk
Moderate Risk
Safe

Use:
- colored background tint
- small dot/icon
- readable label

Never rely on color alone.

---

## 12. Map Visual System

Map overlays are central.

### Route Colors
- safest: accent/safe
- fastest: blue/cyan family
- shortest: neutral bright line
- critical section: danger

### Markers
Use custom marker shapes/icons for:
- accidents
- hazards
- hospitals
- police
- fire service
- user location

Avoid default Leaflet blue pins in the final design.

### Clusters
When many markers exist:
- use cluster counts
- animate cluster expansion gently

---

## 13. Loading States

Preferred:
- skeleton cards
- route tracing animation
- map shimmer overlay
- subtle spinner only when needed

Do not block the entire screen unless action truly requires it.

---

## 14. Empty States

Every empty screen should explain:
- what is missing
- what the user can do next

Example:
“No saved routes yet. Find a route and save it for quick access.”

---

## 15. Design Consistency Rule

Before adding a new visual style:
- check whether an existing token/component already solves it
- avoid one-off CSS
- avoid duplicating button/card styles
- prefer reusable classes/components/partials
