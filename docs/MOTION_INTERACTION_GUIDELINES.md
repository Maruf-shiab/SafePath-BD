# SafePath BD — Motion & Interaction Guidelines

## 1. Motion Goal

Motion should make SafePath BD feel alive, intentional, and premium.

Motion must:
- explain state
- guide attention
- reinforce hierarchy
- improve map interaction
- make transitions feel continuous

Motion must not exist only to “look cool.”

---

## 2. Motion Principles

### Fast for small actions
100–180ms

Examples:
- hover
- button press
- chip selection
- icon state

### Medium for UI transitions
180–320ms

Examples:
- drawer
- modal
- card expansion
- tab change

### Slow for cinematic sections
400–900ms

Examples:
- hero reveal
- large route draw
- section entrance

---

## 3. Easing

Prefer:
- ease-out for entrance
- ease-in for exit
- custom cubic-bezier for premium motion

Example:

```css
--ease-standard: cubic-bezier(0.22, 1, 0.36, 1);
--ease-soft: cubic-bezier(0.25, 0.8, 0.25, 1);
--ease-spring: cubic-bezier(0.16, 1, 0.3, 1);
```

Avoid:
- linear for UI transitions
- cartoon-like overshoot
- huge elastic bounce

---

## 4. Page Transitions

When navigating:
- fade + subtle translate
- maintain continuity
- do not animate entire page dramatically every time

Suggested:
- old content: fade 120ms
- new content: translateY(8px) → 0 with fade 220ms

---

## 5. Scroll Animation

Use sparingly.

Good:
- staggered section reveal
- line/route drawing
- number count-up
- map card entrance

Bad:
- every paragraph flying in
- repeated zoom effects
- random parallax on all elements

---

## 6. Map Motion

### Current Location
- subtle pulse every few seconds
- do not pulse constantly at high intensity

### Marker Appearance
- scale 0.85 → 1
- fade in
- stagger clusters

### Route Selection
- draw polyline progressively
- then settle into static/slow pulse

### Route Switching
- previous route fades
- new route draws
- side panel updates with crossfade

### Risk Segment
On hover/select:
- slightly increase stroke width
- glow
- open detail panel

---

## 7. Panels & Drawers

Desktop:
- slide from right or left
- slight fade
- 240–320ms

Mobile:
- bottom sheet
- spring-like motion
- support drag handle visually

---

## 8. Modals

Use modal only when action blocks current flow.

Animation:
- backdrop fade
- modal scale 0.97 → 1
- 180–220ms

Avoid:
- large bounce
- spinning entrance

---

## 9. Buttons

Hover:
- translateY(-1px)
- subtle shadow increase

Press:
- scale(0.98)

Loading:
- preserve width
- replace label with progress state
- avoid layout shift

---

## 10. Number Animation

Use for:
- safety score
- dashboard counts
- distance
- route duration

Use count-up only when value changes or first appears.

Do not animate every number repeatedly.

---

## 11. Safety Score Animation

Recommended sequence:

1. panel appears
2. ring/arc draws
3. number counts to target
4. factor list fades in
5. highest-risk factor receives subtle emphasis

---

## 12. Report Status Change

Example:
Pending → Verified

Use:
- chip crossfade
- icon morph/fade
- success highlight
- optional small confirmation toast

Avoid full-page reload feeling.

---

## 13. Notification Motion

Toast:
- slide + fade
- short duration
- no excessive stacking

Notification bell:
- small pulse only when new unread item arrives

---

## 14. Reduced Motion

Must support:

```css
@media (prefers-reduced-motion: reduce) {
  * {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }
}
```

For map motion, also disable:
- pulsing
- animated route draw
- parallax

---

## 15. Performance Rule

Prefer:
- transform
- opacity

Avoid animating:
- width
- height
- top
- left
- expensive blur on large areas

Use `will-change` only when truly needed.

---

## 16. Recommended Libraries

Only add a library when needed.

Possible:
- GSAP for complex hero/scroll sequences
- Motion One / Web Animations API for lighter effects
- Leaflet for map interactions

Do not add multiple animation libraries that overlap heavily.

---

## 17. AI Rule

If an animation does not improve:
- hierarchy
- clarity
- continuity
- feedback

do not add it.

The goal is “highly polished,” not “maximum number of animations.”
