# SafePath BD — AI Frontend Rules

## 1. Purpose

This file gives strict instructions to GitHub Copilot / Opus when generating frontend code.

The project must not look like a generic AI-generated or “vibe coded” website.

---

## 2. Mandatory Reading

Before implementing frontend work, read:

- `UI_UX_VISION.md`
- `DESIGN_SYSTEM.md`
- `MOTION_INTERACTION_GUIDELINES.md`
- `COMPONENT_SPECS.md`
- `FRONTEND_IMPLEMENTATION_GUIDE.md`

Also respect:
- `ARCHITECTURE.md`
- `CODING_GUIDELINES.md`
- `FEATURES.md`

---

## 3. Forbidden Patterns

Do NOT generate:

- generic SaaS hero
- purple/blue gradient blob background
- giant centered headline + 3 generic cards
- 4 identical stat cards everywhere
- default Bootstrap navbar
- default Bootstrap cards
- random Lucide icon in every heading
- glassmorphism on every surface
- exaggerated border-radius
- neon gradients
- dozens of animated particles
- random floating blobs
- every section using same 3-column layout
- unnecessary “AI” visual clichés
- huge text saying “revolutionize your journey”
- lorem ipsum or fake production statistics

---

## 4. Visual Quality Requirement

Every page must have:
- clear visual hierarchy
- intentional spacing
- consistent tokens
- responsive behavior
- custom interaction states
- loading/error/empty states where relevant
- accessible contrast
- polished motion

---

## 5. Motion Rule

Do not add motion just because the user requested “many animations.”

Instead:
- design a motion system
- reuse timing/easing
- use meaningful transitions
- keep performance high
- respect reduced-motion

---

## 6. Map Rule

Leaflet/OpenStreetMap should be visually customized.

Do not leave:
- default marker icons
- default popup styling
- default layer controls
- raw unstyled routing output

Wrap map UI in SafePath BD's visual system.

---

## 7. Page Review Checklist

Before declaring a page complete, ask:

- Does this look like SafePath BD?
- Could this be confused with a default dashboard template?
- Are the animations purposeful?
- Is the mobile layout intentional?
- Are interactions smooth?
- Is the map still the focus where appropriate?
- Are risk states visually clear?
- Is content readable?
- Are colors being used semantically?

If the answer is not strong, improve the page before moving on.

---

## 8. Implementation Discipline

Do not redesign the backend architecture for frontend work.

Do not modify the database schema for UI convenience.

Do not add a new frontend framework unless explicitly approved.

Use the existing ASP.NET Core MVC architecture.

---

## 9. Final Goal

The interface should feel:
- designed
- authored
- coherent
- map-native
- safety-focused
- premium
- modern

It should not feel like a collection of AI-generated sections.
