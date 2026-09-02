# MySelf App — 07 Testing And Learning

> Split documentation file. Purpose: Testing strategy, definition of done, staged testing progression and the AI-assisted .NET learning protocol.
> Original source: `myself-app-blueprint-v1.1-learning-r1.md`

## 16. Testing strategy

### Backend

- Unit tests for BMI, BMR, TDEE, macros, volume, e1RM, PR detection, workout frequency and calendar-date grouping.
- Unit tests for every onboarding goal branch, estimate breakdown, formula version and effective-dated goal history.
- Unit tests proving workout calories are not automatically added to the daily nutrition budget.
- Unit tests for tracking-mode required fields, strict set validation, saved-meal scaling and copy-vs-move semantics.
- Unit tests for bodyweight rep trends, extra-load volume and assisted-progress direction.
- Unit tests for Standard/Drop, AMRAP, failure flag, skip exclusion and total unilateral reps.
- Unit tests proving superset grouping never changes per-exercise volume, e1RM or PR results.
- Unit tests for unequal superset set counts, round completion and rest-after-round triggering.
- Unit tests enforcing one active session while allowing multiple completed sessions per day.
- Integration tests with real PostgreSQL test container.
- Transaction tests proving bulk operations fully roll back on one invalid selected item.
- Auth tests for refresh rotation, owner isolation and expired sessions.
- Contract tests for USDA/OFF adapters using saved fixtures, not live network on every run.

### Frontend

- Vitest + React Testing Library for forms, hooks and critical components.
- Playwright E2E for register → onboarding → plan → workout → dashboard.
- E2E for Lose/Maintain/Gain/Track-only branches, manual target and skipped nutrition setup.
- E2E for under-18 and calculation-opt-out flows producing no automatic target.
- Separate E2E for meal logging and daily totals.
- E2E for multi-select copy/cut/paste and undo in programs and nutrition days.
- E2E for adding a Saved Meal, scaling it, editing one item and preserving the template.
- E2E for local workout recovery after refresh/offline, later sync and conflict recovery.
- E2E for add/replace exercise today-only versus future-template update.
- E2E for manual variant selection with no preselection/suggestion, calendar logging and completed-session date editing.
- E2E for attempting to start a second active session and choosing resume/finish/discard.
- E2E for create/reorder/ungroup superset and for active-workout round progression with skipped sets.
- E2E proving completed-session edits recalculate and revoke/reassign affected PRs.
- Accessibility checks on login, active workout and meal log flow.

### Testing learning progression

Η παραπάνω testing strategy είναι ο στόχος της ολοκληρωμένης εφαρμογής, όχι απαίτηση να μάθουμε όλα τα testing εργαλεία πριν ξεκινήσει το feature development. Τα εργαλεία εισάγονται όταν υπάρχει πραγματικό use case.

1. **Stage 1 — xUnit unit tests:** pure calculations, validation και deterministic domain rules.
2. **Stage 2 — ASP.NET Core integration tests:** endpoint + application flow χωρίς browser.
3. **Stage 3 — PostgreSQL integration tests:** πραγματική relational συμπεριφορά με disposable/test container database.
4. **Stage 4 — Vitest + React Testing Library:** critical forms/components/client behaviour όπου προσθέτει αξία.
5. **Stage 5 — Playwright E2E:** critical user journeys που διασχίζουν UI, API, auth και persistence.

Δεν κυνηγάμε coverage percentage για το portfolio. Προτεραιότητα έχουν tests που προστατεύουν πραγματικούς business rules και regressions.

### Definition of done

- Acceptance criteria pass.
- Loading, empty, error and success states exist.
- Mobile 360 px and desktop tested.
- No secrets committed.
- Migration and tests run in CI.
- OpenAPI spec updated.

### Learning definition of done

Για κάθε backend vertical slice ισχύουν επιπλέον:

- Ο developer μπορεί να εξηγήσει το request/data flow χωρίς να διαβάζει απάντηση του Claude.
- Ο developer καταλαβαίνει τον ρόλο κάθε νέας class/interface που προστέθηκε.
- Ο developer μπορεί να εξηγήσει γιατί κάθε νέο dependency είναι registered στο DI container.
- Ο developer μπορεί να εντοπίσει Entity, DTO/contract, validation και database operation του slice.
- Το happy path και τουλάχιστον ένα failure/edge path έχουν ελεγχθεί χειροκίνητα.
- Τουλάχιστον ένα σημαντικό automated test έχει γίνει intentional fail μία φορά ώστε να επιβεβαιωθεί ότι προστατεύει το αναμενόμενο behaviour.
- Δεν μένει merged code που ο developer δεν μπορεί να εξηγήσει σε γενικές γραμμές. Δεν απαιτείται αποστήθιση syntax ή framework API.

## 16A. Learning and AI-assisted development protocol

Το MySelf είναι ταυτόχρονα product project και **backend learning project**. Το Claude λειτουργεί ως senior pair programmer / tutor και όχι ως autonomous project generator.

### Developer background

Ο developer είναι ήδη άνετος με:

- JavaScript / TypeScript,
- React και frontend development,
- REST APIs και βασικά web concepts,
- authentication concepts,
- βασικές database έννοιες και manual/exploratory testing.

Ο developer είναι νέος σε:

- C#,
- ASP.NET Core,
- Entity Framework Core,
- ASP.NET Core Identity,
- professional .NET solution/project structure.

Στην αρχή είναι αποδεκτό το Claude να γράφει σημαντικό μέρος του C# syntax/boilerplate, αλλά ο developer πρέπει σταδιακά να αποκτά ownership του implementation και να μπορεί να γράφει μικρές classes, functions, endpoints και tests με λιγότερη βοήθεια.

### Ownership boundaries

**Ο developer αποφασίζει:**

- product behaviour,
- acceptance criteria,
- expected results,
- API behaviour/contracts,
- edge cases,
- test scenarios και τι θεωρείται σωστό αποτέλεσμα.

**Το Claude μπορεί να βοηθά με:**

- C# syntax και boilerplate,
- ASP.NET Core conventions,
- EF Core configuration/migrations,
- test implementation,
- framework-specific debugging,
- refactoring αφού πρώτα εξηγηθεί η ανάγκη.

Αν το behaviour είναι ambiguous ή το blueprint δεν ορίζει την απάντηση, το Claude **σταματά και ρωτά** αντί να εφευρίσκει requirement.

### Mandatory workflow for every vertical slice

#### Step 1 — Analyze before coding

Πριν αλλάξει οποιοδήποτε αρχείο, το Claude πρέπει να:

1. εντοπίσει τα σχετικά blueprint requirements και acceptance criteria,
2. επαναδιατυπώσει το behaviour που θα υλοποιηθεί,
3. προτείνει τα files που θα δημιουργηθούν/αλλάξουν,
4. εξηγήσει το request/data flow end-to-end,
5. εξηγήσει κάθε νέο C#/.NET concept,
6. συγκρίνει άγνωστες έννοιες με TypeScript/JavaScript όπου βοηθά,
7. εξηγήσει database schema/query/migration αλλαγές,
8. προτείνει tests και τι regression θα προστατεύει το καθένα.

Δεν γράφει code μέχρι να ολοκληρωθεί αυτή η εξήγηση. Εκτός αν ο developer ζητήσει ρητά να συνεχίσει, περιμένει approval πριν το implementation.

#### Step 2 — Implement the smallest useful slice

- Υλοποιείται μόνο το συμφωνημένο behaviour.
- Δεν υλοποιούνται unrelated future features.
- Δεν γίνεται silent refactor άσχετου κώδικα.
- Προτιμάται απλό, conventional ASP.NET Core από clever/ceremonial architecture.
- Design patterns και abstractions προστίθενται μόνο όταν λύνουν συγκεκριμένο πρόβλημα που έχει ήδη εξηγηθεί.
- Locked product decisions δεν αλλάζουν.

#### Step 3 — Teach the implementation

Μετά το implementation, το Claude πρέπει να:

1. απαριθμήσει όλα τα changed files και τον ρόλο τους,
2. κάνει walkthrough του σημαντικού code,
3. εξηγήσει νέο C# syntax,
4. εξηγήσει ξανά το πλήρες request/data flow,
5. εξηγήσει conceptually τι κάνει το EF Core προς PostgreSQL,
6. επισημάνει framework conventions που δεν είναι obvious από TypeScript/React background.

Στο τέλος κάθε working session (όχι κάθε μεμονωμένου slice), αυτή η εξήγηση γράφεται επίσης
ως μόνιμο αρχείο στο `docs/learning-log/YYYY-MM-DD-<topic>.md`, ώστε να μένει διαθέσιμη
πέρα από το chat scrollback. Βλέπε `docs/learning-log/README.md` για το index.

#### Step 4 — Manual verification first

Το Claude δίνει exact commands και manual verification plan με:

- ένα happy path,
- τουλάχιστον ένα invalid/failure path,
- expected HTTP response / UI behaviour / database effect.

Ο developer εκτελεί το flow και επιβεβαιώνει actual vs expected behaviour πριν θεωρηθεί ολοκληρωμένο το slice.

#### Step 5 — Automated tests

Πριν γράψει tests, το Claude εξηγεί για κάθε test:

- ποιο behaviour προστατεύει,
- γιατί είναι unit, integration ή E2E,
- Arrange / Act / Assert,
- ποιο regression θα έπιανε.

Μετά υλοποιεί μόνο τα συμφωνημένα tests. Δεν αλλάζει production behaviour απλώς για να γίνει ένα failing test green χωρίς πρώτα να διαγνώσει και να εξηγήσει αν πρόκειται για app bug, test bug, flaky test ή environment issue.

Μετά από σημαντικό test, προτείνει μία ασφαλή προσωρινή αλλαγή που πρέπει να κάνει το test fail, ώστε να επαληθευτεί ότι το test πραγματικά προστατεύει το behaviour.

#### Step 6 — Knowledge check

Όταν ένα slice εισάγει νέο .NET concept, το Claude κάνει 3–5 σύντομες ερωτήσεις κατανόησης. Δεν χρειάζεται αποστήθιση syntax· ο στόχος είναι ο developer να μπορεί να εξηγήσει:

- τι κάνει το feature,
- ποια classes συμμετέχουν και γιατί,
- πώς φτάνει το request στη database και πίσω,
- τι αποδεικνύει το βασικό test,
- ποιο edge case θεωρεί σημαντικό.

### Progressive independence target

Στις πρώτες φάσεις το Claude μπορεί να παράγει το μεγαλύτερο μέρος του framework-specific syntax. Καθώς προχωρά το project, ο developer πρέπει να αναλαμβάνει σταδιακά περισσότερα μικρά C# tasks μόνος του. Ο στόχος μέχρι το τέλος του MVP είναι να μπορεί χωρίς code generation να γράψει ή να τροποποιήσει ένα απλό C# method, μία μικρή class/DTO, ένα απλό endpoint και ένα βασικό xUnit test, χρησιμοποιώντας documentation όταν χρειάζεται.
