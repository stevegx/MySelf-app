# 2026-09-08 — Accessibility pass (Phase 6, slice 2)

Branch `phase-6-polish-release`. Commits `fea5893`, `a16be94`, `ed8ef2f`.

`docs/08` Phase 6: "Accessibility, localization, export/delete, rate limiting."
This is the accessibility sweep, in three slices. Scope: keyboard operability,
focus management, screen-reader semantics. Colour contrast is **not** touched —
colours are frozen pending a design decision.

## Slice 2a — accessible dialogs (`fea5893`)

Every dialog in the app was an ad-hoc block:

```tsx
<div className="fixed inset-0 …" role="dialog" aria-modal="true"
     aria-label={…} onKeyDown={(e) => e.key === "Escape" && onClose()}>
```

`aria-modal="true"` tells assistive tech "focus is contained here" — but nothing
actually contained it. Tab walked straight out into the page behind; opening a
dialog didn't move focus; closing it didn't put focus back.

**`useFocusTrap(onClose, enabled = true)`** (`src/lib/useFocusTrap.ts`) — one
hook, all the behaviour:

- on mount: remember `document.activeElement`, move focus to the first focusable
  inside the container (unless something inside is already focused, so a child's
  `autoFocus` still wins), and **lock body scroll** (reference-counted, so
  stacked overlays don't stomp each other's `overflow`).
- a `focusin` listener on `document` that yanks focus back if it escapes.
- on unmount: unlock scroll, `previouslyFocused.focus()` (guarded by
  `document.contains` — the trigger may be gone).
- returns `{ ref, onKeyDown }`. `onKeyDown` closes on Escape and traps Tab:
  compute the focusables, and on Tab-past-last or Shift+Tab-past-first,
  `preventDefault()` and wrap.

**`Modal`** (`components/ui/Modal.tsx`) uses the hook and adds the chrome:
`createPortal` to `<body>`, `role="dialog"` + `aria-modal`, and the name. The
name is the interesting bit — some dialogs need an accessible name that differs
from the visible heading (`aria-label="Add food to Lunch"` while the heading
reads "Add food · Lunch"). So:

- `title` (required) → always the visible `<h2>`.
- `label` (optional) → when set, becomes `aria-label` and the `<h2>` is no longer
  referenced by `aria-labelledby`. When absent, `aria-labelledby` points at the
  `<h2>`.

This kept every existing `getByRole("dialog", { name: … })` test green without
changing what a screen reader announces.

Migrated: `AddFoodDialog`, `ManageCategoriesDialog`, the saved-meal add/save
dialogs, the bulk move/copy slot picker, `LogWeightDialog`, `useConfirm`. Dialog
error text moved into `role="alert"`.

`ExercisePicker`'s slide-in **drawer** is a different shape (right-aligned,
full-height, its own header) — wrapping it in `Modal` would have re-centred it.
Instead it calls `useFocusTrap(onClose, drawer)` directly (the `enabled` flag is
why the hook takes one — the same component also renders inline, where a focus
trap would be wrong) and portals itself.

New React notes:
- `createPortal(node, document.body)` renders `node` elsewhere in the DOM while
  keeping it in the React tree (context, events bubble through React). Testing
  Library's `screen` still finds it.
- jsdom has no layout, so `el.offsetParent` is always `null` and
  `getClientRects()` is always empty — a visibility filter built on either hides
  *everything* in tests. The trap filters on `hidden` / `aria-hidden` instead.

Tests: `Modal.test.tsx` — name from title, name override, Tab wrap both ways,
Escape + focus restore, scroll lock/release.

## Slice 2b — form errors (`a16be94`)

Auth forms already had `noVadidate`, a `role="alert"` form-level banner, and
`aria-invalid` on inputs. The gap: the per-field error text was a loose `<span>`
not connected to its input, so focusing an invalid field announced nothing.

**`Field` gains `error`.** When set it (a) replaces the hint, (b) renders in
`role="alert"`, (c) — via `cloneElement` on the single child — sets
`aria-describedby` (merged with any existing) and `aria-invalid` on the actual
control. A plain `hint` now gets the same `aria-describedby` wiring. All ~28
existing `<Field>`s benefit with no change.

Auth screens moved from `hint={errors.x ? <span> : "rule text"}` to
`hint="rule text"` + `error={errors.x?.message}`.

Also: `Segmented` → `role="radiogroup"` (was a generic `group`); `StepperInput`
buttons → "Decrease X" / "Increase X".

New React note: `cloneElement(el, extraProps)` returns a copy of an element with
merged props — the way a wrapper component can decorate an arbitrary child it
doesn't own. Guard with `isValidElement` first.

Tests: `Field.test.tsx` — label association, hint `aria-describedby`, error takes
over + `aria-invalid` + alert.

## Slice 2c — headings, route announcer, reduced motion (`ed8ef2f`)

- **One `<h1>` per page.** Nothing rendered an `<h1>` — `PageHeader` and the
  standalone screens all used `<h2>`/`<h3>`. Promoted the page title to `<h1>`
  in `PageHeader`, `SettingsScreen`, the four auth screens and the onboarding
  wizard. `h1` base is 34px vs the design's 26px title, so each carries
  `text-[26px]` — a pure-semantics change, no visual shift. (`h1` and `h2` share
  the display font, so the typeface is unchanged too.) Sub-section `<h3>`s under
  the new `<h1>` technically skip a level now — noted, not chased, because
  bumping them to `<h2>` would flip them to the Caprasimo display face.
- **`RouteAnnouncer`** in `AppShell`: a visually-hidden `aria-live="polite"`
  region. A client-side route change moves no focus and makes no sound; on
  `pathname` change it reads the new page's `<h1>` (after a `requestAnimationFrame`
  so the route has rendered) and announces "<title> page".
- **`prefers-reduced-motion: reduce`** in `theme.css` — a global rule cutting
  `animation-duration` / `transition-duration` to ~0 and forcing
  `scroll-behavior: auto`. Kills the skeleton pulse, dialog/hover transitions and
  smooth scrolling for users who ask the OS for it.

## Still open (not done in this pass)

- Colour-contrast audit — blocked on the colour freeze.
- Localization (the other Phase 6 string) — English only per decision #2.
- A few `div`-grid "tables" (history rows, coverage bars) could take real table
  semantics.
- The two `<nav aria-label="Primary">` (mobile + desktop) — only one is in the
  a11y tree per breakpoint, so low priority.
