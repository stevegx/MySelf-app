# MySelf App — 06 Design Validation Safety

> Split documentation file. Purpose: Design system, themes/tokens, accessibility-oriented UI rules, validation, edge cases and safety rules.
> Original source: `myself-app-blueprint-v1.1-learning-r1.md`

## 14. Design system

### Visual direction

**Personal clarity**: καθαρό, σύγχρονο και ουδέτερο UI που δεν μοιάζει αποκλειστικά με bodybuilding app. Light theme ως default, πλήρες dark theme ως επιλογή, διακριτικό indigo primary, αρκετός χώρος και μεγάλα tap targets. Το design δεν χρησιμοποιεί gender-coded χρώματα ή επιθετικό gym imagery.

### Locked brand direction — Balanced Indigo

Balanced Indigo γίνεται η βασική κατεύθυνση. Είναι αρκετά ουδέτερο για workout, nutrition και progress, χωρίς να μοιάζει ούτε με clinical health app ούτε με hardcore gym app.

#### Indigo brand scale

| Step | Hex | Main use |
| ---: | --- | --- |
| 50 | `#EEF2FF` | Selected/soft background |
| 100 | `#E0E7FF` | Hovered soft background |
| 200 | `#C7D2FE` | Decorative subtle accent |
| 300 | `#A5B4FC` | Dark-theme focus/secondary accent |
| 400 | `#818CF8` | Dark-theme primary |
| 500 | `#6366F1` | Charts/brand accent |
| 600 | `#4F46E5` | Light-theme primary |
| 700 | `#4338CA` | Light-theme hover |
| 800 | `#3730A3` | Light-theme pressed |
| 900 | `#312E81` | Strong brand surface |
| 950 | `#1E1B4B` | Deep indigo surface |

Το primary χρησιμοποιείται για main actions, active navigation και selected states. Η επιτυχία/βελτίωση έχει ξεχωριστό green token, ώστε το indigo να μην αποκτά δύο διαφορετικές σημασίες.

### Complete light theme tokens

| Token | Hex | Use |
| --- | --- | --- |
| `background` | `#F7F8FC` | App background |
| `surface` | `#FFFFFF` | Cards, sheets, navigation |
| `surface-subtle` | `#F1F3F8` | Inputs, grouped rows, inactive areas |
| `surface-strong` | `#E9ECF3` | Pressed neutral surface |
| `text-primary` | `#182033` | Main content |
| `text-secondary` | `#667085` | Supporting content |
| `text-disabled` | `#98A2B3` | Disabled/placeholder only |
| `border` | `#E2E6EE` | Default separators |
| `border-strong` | `#C7CED9` | Focused grouping/table header |
| `primary` | `#4F46E5` | Main CTA, active navigation |
| `primary-hover` | `#4338CA` | Hover |
| `primary-pressed` | `#3730A3` | Pressed |
| `primary-soft` | `#EEF2FF` | Selected row/background |
| `on-primary` | `#FFFFFF` | Text/icons on primary |
| `focus-ring` | `#818CF8` | Keyboard focus |
| `success` | `#15803D` | Confirmed improvement/success |
| `success-soft` | `#DCFCE7` | Success background |
| `info` | `#0369A1` | Informational status |
| `info-soft` | `#E0F2FE` | Informational background |
| `warning` | `#B45309` | Warning/attention |
| `warning-soft` | `#FEF3C7` | Warning background |
| `danger` | `#B42318` | Error/delete |
| `danger-soft` | `#FEE4E2` | Error background |

### Complete dark theme tokens

| Token | Hex | Use |
| --- | --- | --- |
| `background` | `#0F1420` | App background |
| `surface` | `#171D2B` | Cards, sheets, navigation |
| `surface-subtle` | `#20283A` | Inputs, grouped rows |
| `surface-strong` | `#2A3447` | Pressed neutral surface |
| `text-primary` | `#F7F8FC` | Main content |
| `text-secondary` | `#A5AEC0` | Supporting content |
| `text-disabled` | `#6F7A90` | Disabled/placeholder only |
| `border` | `#303A4F` | Default separators |
| `border-strong` | `#46526A` | Strong separator/focus grouping |
| `primary` | `#818CF8` | Main CTA, active navigation |
| `primary-hover` | `#A5B4FC` | Hover |
| `primary-pressed` | `#6366F1` | Pressed |
| `primary-soft` | `#252A55` | Selected row/background |
| `on-primary` | `#11162A` | Text/icons on primary |
| `focus-ring` | `#A5B4FC` | Keyboard focus |
| `success` | `#4ADE80` | Confirmed improvement/success |
| `success-soft` | `#163A29` | Success background |
| `info` | `#38BDF8` | Informational status |
| `info-soft` | `#123548` | Informational background |
| `warning` | `#FBBF24` | Warning/attention |
| `warning-soft` | `#493711` | Warning background |
| `danger` | `#FB7185` | Error/delete |
| `danger-soft` | `#4A202A` | Error background |

### Data visualization tokens

| Metric | Light | Dark | Rule |
| --- | --- | --- | --- |
| Strength / primary trend | `#4F46E5` | `#818CF8` | Main workout trend |
| Protein | `#2563EB` | `#60A5FA` | Stable mapping everywhere |
| Carbohydrates | `#D97706` | `#FBBF24` | Stable mapping everywhere |
| Fat | `#DB2777` | `#F472B6` | Stable mapping everywhere |
| Calories / energy | `#7C3AED` | `#C4B5FD` | Energy totals/budget |
| Positive change | `#15803D` | `#4ADE80` | Only confirmed improvement |
| Negative/error | `#B42318` | `#FB7185` | Error or explicit decline, never normal fluctuation |

Χρώμα μόνο του δεν μεταφέρει νόημα. Κάθε chart/status συνδυάζει colour με label, icon, line style ή signed value.

### UI rules

- Font: Inter ή system sans-serif.
- Base spacing grid: 4 px; common gaps 8/12/16/24/32.
- Radius: 12 px cards, 10 px controls, pill only για tags/status.
- Minimum tap target: 44 × 44 px.
- Charts never rely only on colour: labels, shapes and accessible descriptions.
- Protein/carbs/fat keep the same chart color mapping everywhere.
- Use skeletons for first load, inline errors for forms, toast only for transient confirmation.
- Launch copy in English. i18n-ready from day one so Greek can be added without component rewrites; no text embedded in reusable components.
- Visual hierarchy comes from spacing, type and contrast—not from excessive cards or colour.
- Every primary task should be understandable without fitness jargon; advanced fields such as RIR are optional and explained.

## 15. Validation, edge cases and safety

- Metric and imperial entry, canonical storage in metric.
- Decimal plates such as 1.25 kg.
- Bodyweight exercises: optional external load and optional assisted load.
- Unilateral exercises use **total reps across both sides**. UI label: `Total reps`; helper example: `10 per side = 20 total`.
- Failed/interrupted workout can remain `InProgress`, be resumed or discarded.
- Only one workout may be `InProgress` per user. Multiple completed sessions on the same calendar day are allowed.
- Starting another workout while one is active requires explicit `Resume current`, `Finish current` or `Discard and start new`.
- No planned occurrence or missed-workout entity exists in the MVP. Calendar entries are created from actual sessions.
- A completed session date can be edited; calendar grouping and all date-range analytics recalculate accordingly.
- Deleting a workout variant that has history archives the variant; it never deletes completed workout logs automatically.
- `Weight × Reps` sets require both fields; missing fields block completion with inline validation.
- Bodyweight, assisted and timed exercises use explicit MVP tracking modes instead of artificial zero weights; distance + duration follows in V1.
- Bodyweight exercises never add the user's body weight to load, volume or e1RM calculations; only reps and optional extra/assistance weight are tracked.
- Creating or removing a superset changes grouping/order only and never changes exercise prescriptions or calculated performance metrics.
- A superset round completes when every set scheduled for that round is either completed or explicitly skipped; only then may its group rest timer auto-start.
- User can log an ad-hoc workout without an active program.
- Meal units normalize to grams where possible; original serving stays visible.
- Missing nutrition data is shown as unknown, not zero.
- External food result displays source and last fetched time.
- Copying a saved meal or nutrition day creates snapshots; later template edits do not rewrite past days.
- Bulk paste into a non-empty target asks `append`, `replace selected` or `cancel`; never silently overwrites.
- Editing a completed session triggers deterministic recalculation of derived metrics/PRs before the new version becomes visible.
- Local offline drafts include a revision id; conflicting local/server revisions never silently overwrite each other.
- No calorie recommendation for under-18 users in MVP.
- Pregnancy, eating-disorder risk, kidney disease or clinician-managed diet: manual targets + professional guidance message.
- Never shame the user for workout frequency or exceeded calories; language stays neutral.
