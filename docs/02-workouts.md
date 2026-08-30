# MySelf App — 02 Workouts

> Split documentation file. Purpose: Workout planning, program structure, logging, session behaviour, progression, metrics and offline workout rules.
> Original source: `myself-app-blueprint-v1.1-learning-r1.md`

## 6. Workout planning logic

### Custom-first program builder

Η κύρια ροή δεν ξεκινά από έτοιμο split. Ξεκινά από ένα κενό πρόγραμμα:

1. `Create program` και όνομα.
2. `Add workout group`, π.χ. Push, Pull ή Legs.
3. `Add workout variant`, π.χ. Legs #1 ή Legs #2.
4. Όνομα και σειρά εμφάνισης του variant μέσα στο πρόγραμμα.
5. Αναζήτηση ή δημιουργία exercise.
6. Sets, rep range, target weight, optional RIR και rest time.
7. Reorder με drag-and-drop ή accessible move buttons.
8. Review και `Activate program`.

Ο χρήστης έχει μόνο **ένα active program**. Μπορεί να το αλλάζει πλήρως ή να ενεργοποιήσει άλλο draft/archived program, οπότε το προηγούμενο παύει να είναι active χωρίς να χάνει history.

PPL, Upper/Lower, Full Body και Bro Split μπορούν να προστεθούν αργότερα ως **optional starter templates**. Ποτέ δεν περιορίζουν τον χρήστη ούτε αλλάζουν το custom πρόγραμμα χωρίς επιβεβαίωση.

### Program, workout type and workout variant

Το πρόγραμμα χωρίζεται σε τρία επίπεδα:

```text
Workout Program: PPL — 6 days
├── Push
│   ├── Push #1
│   └── Push #2
├── Pull
│   ├── Pull #1
│   └── Pull #2
└── Legs
    ├── Legs #1
    └── Legs #2
```

- **Workout Program**: το συνολικό πρόγραμμα που σχεδιάζει ο χρήστης, π.χ. `PPL — 6 days`.
- **Workout Type/Group**: λογική κατηγορία μέσα στο πρόγραμμα, π.χ. `Legs`.
- **Workout Variant**: συγκεκριμένη λίστα ασκήσεων και set targets, π.χ. `Legs #1` ή `Legs #2`.

Δεν υπάρχει `ScheduleSlot` στο MVP. Η σειρά των groups/variants χρησιμοποιείται μόνο για οργάνωση του builder και του manual picker, ποτέ ως αυτόματη πρόταση. Το `#1/#2` είναι απλώς καθαρό default naming όταν υπάρχουν πολλαπλές εκδοχές του ίδιου workout type.

### Starting a workout

Η ροή είναι:

1. Ο χρήστης πατά `Start workout`.
2. Το app ανοίγει manual picker με groups και variants του active program, μαζί με επιλογή `Ad-hoc workout`.
3. Ο χρήστης επιλέγει ο ίδιος το variant· δεν υπάρχει preselected ή suggested next workout.
4. Εμφανίζονται οι ακριβείς ασκήσεις, prescriptions και previous values του επιλεγμένου variant.
5. Αν υπάρχει ήδη active session, εμφανίζονται `Resume current`, `Finish current` και `Discard and start new` αντί να δημιουργηθεί δεύτερο active session.
6. Με το start δημιουργείται session snapshot ώστε μελλοντικές αλλαγές στο πρόγραμμα να μην επηρεάζουν το ιστορικό.

Έτσι, αν επιλέξει `Legs #1`, βλέπει μόνο το exercise list του `Legs #1`. Την επόμενη φορά μπορεί να επιλέξει χειροκίνητα `Legs #2` ή οποιοδήποτε άλλο variant.

### Program builder

Κάθε workout day έχει:

- όνομα και display order,
- estimated duration,
- ordered exercises,
- standard working sets,
- optional `AMRAP`, `Drop set` και `To failure` settings,
- target reps ως range, π.χ. 6–8,
- target load προαιρετικά,
- RIR προαιρετικά,
- rest seconds,
- notes,
- superset group προαιρετικά.

Δεν υπάρχει warm-up set type. Αν ο χρήστης κάνει warm-up, δεν το καταγράφει ως performed set στο MVP.

### Supersets in the program builder

Το superset είναι **execution grouping**, όχι διαφορετικός τύπος set και όχι διαφορετικός τρόπος μέτρησης.

- Ο χρήστης επιλέγει δύο ή περισσότερες ασκήσεις και πατά `Create superset`.
- Τα μέλη γίνονται συνεχόμενο block και εμφανίζονται ως `A1`, `A2`, προαιρετικά `A3` κ.ο.κ. Το επόμενο group γίνεται `B1`, `B2`.
- Μπορεί να αλλάξει τη σειρά μέσα στο group, να μετακινήσει ολόκληρο το group, να προσθέσει/αφαιρέσει μέλος ή να πατήσει `Ungroup`.
- Κάθε άσκηση κρατά ανεξάρτητα τα δικά της sets, reps, load, RIR, set options και tracking mode.
- Επιτρέπονται διαφορετικοί αριθμοί sets. Ο αριθμός των rounds είναι ο μεγαλύτερος αριθμός sets ανάμεσα στα μέλη· άσκηση χωρίς set στο συγκεκριμένο round απλώς δεν εμφανίζει βήμα.
- Το group έχει `restAfterRoundSeconds`. Δεν ξεκινά rest timer ανάμεσα στα μέλη του ίδιου round· ξεκινά αφού ολοκληρωθούν ή γίνουν skip όλα τα προβλεπόμενα sets του round.
- Το `Skip set` λειτουργεί κανονικά και επιτρέπει στο round να προχωρήσει όταν όλα τα υπόλοιπα μέλη έχουν ολοκληρωθεί ή παραλειφθεί.
- Στο active workout ο χρήστης μπορεί να φύγει από την προτεινόμενη σειρά, αλλά το UI κρατά ορατό ποια μέλη του round απομένουν.
- Το session snapshot αποθηκεύει το group και τη σειρά όπως εκτελέστηκαν, ώστε μελλοντικό ungroup ή reorder του template να μην αλλάζει το ιστορικό.

Τα analytics παραμένουν **ανά exercise και performed set**. Το superset group δεν πολλαπλασιάζει, συνδυάζει ή τροποποιεί volume, reps, extra weight, e1RM ή PRs. Μπορεί αργότερα να εμφανίζεται μόνο ως context, π.χ. `Performed in superset`, χωρίς ξεχωριστό performance score.

### Progressive disclosure in the builder

Η βασική exercise row δείχνει μόνο exercise, sets, weight/reps target και rest time. Το `More options` ανοίγει:

- RIR,
- AMRAP,
- Drop set,
- Target to failure,
- rep range,
- notes,
- tempo,
- progression settings,
- superset group και `rest after round`.

Δεν υπάρχει ξεχωριστό global Basic/Advanced mode. Όλα μένουν editable ανά exercise χωρίς να φορτώνεται ο απλός χρήστης με όλα τα fields ταυτόχρονα.

### Progression rule για V1

Αρχικά υποστηρίζουμε **double progression**:

- Ο χρήστης έχει rep range, π.χ. 3 × 8–10.
- Όταν ολοκληρώσει όλα τα working sets στο πάνω όριο με το απαιτούμενο RIR, το app προτείνει αύξηση βάρους.
- Η αύξηση είναι configurable, π.χ. +2,5 kg upper body και +5 kg lower body.
- Το app προτείνει, δεν αλλάζει μόνο του το επόμενο workout.

## 7. Workout logging και metrics

### Logging UX

- Με το start δημιουργείται immutable snapshot του προγράμματος για το session.
- Κάθε set δείχνει target και previous performance.
- `Complete` με ένα tap, edit weight/reps inline, copy previous set.
- Για standalone exercise, το rest timer ξεκινά προαιρετικά μετά το complete. Για superset ακολουθεί το rule ολοκλήρωσης του round.
- Το rest timer λειτουργεί μέσα στο app με optional sound/vibration όσο η εφαρμογή είναι ανοιχτή. Δεν ζητά permission και δεν στέλνει browser notifications στο MVP.
- Το workout autosaves σε κάθε αλλαγή και μπορεί να γίνει resume.
- Στο finish εμφανίζεται summary: duration, volume, completed sets, PRs, notes.
- `Skip set` αφήνει το set εκτός analytics και δέχεται optional reason: pain, equipment, time ή other.
- `Add exercise` προσθέτει exercise μόνο στο σημερινό session και μετά το finish μπορεί να προτείνει `Also add to this workout variant`.
- `Replace exercise` προσφέρει `Today only` ή `Today and future workouts`.
- Σε superset, τα μέλη εμφανίζονται μαζί ανά round και το rest timer ξεκινά μετά το τελευταίο completed/skipped μέλος του round.
- Completed workout logs παραμένουν editable. Κάθε edit επανυπολογίζει volume, e1RM, PRs και dashboard/progress charts και σημειώνει το session ως `Edited`.

### Offline local autosave

- Κάθε αλλαγή του active workout αποθηκεύεται άμεσα σε local draft και συγχρονίζεται στον server όταν υπάρχει σύνδεση.
- Το ίδιο ισχύει για unfinished meal draft.
- UI states: `Saving locally`, `Saved locally`, `Syncing`, `Synced`, `Sync error`.
- Browser refresh/crash επαναφέρει το draft.
- Αν server και local draft έχουν αλλάξει, δεν γίνεται silent overwrite· εμφανίζεται conflict recovery flow.
- Full offline browsing/editing όλης της εφαρμογής δεν είναι MVP.

### Strict set validation

Για exercise tracking mode `Weight × Reps`, και τα δύο values είναι υποχρεωτικά. Δεν υπάρχει automatic inheritance από το προηγούμενο workout.

- Previous performance εμφανίζεται σε ξεχωριστή στήλη/γραμμή μόνο ως reference.
- Αν υπάρχει weight αλλά λείπουν reps, το set δεν ολοκληρώνεται.
- Αν υπάρχουν reps αλλά λείπει weight, το set δεν ολοκληρώνεται.
- Αν λείπουν και τα δύο, το set δεν ολοκληρώνεται.
- Inline error: `Enter both weight and reps to complete this set.`
- Το focus μεταφέρεται στο πρώτο missing field και τα missing fields αποκτούν error state.
- Το action `Copy previous set` μπορεί να συμπληρώσει και τα δύο fields, αλλά ο χρήστης επιβεβαιώνει/αλλάζει τις τιμές πριν πατήσει complete.
- Αν δεν ολοκληρωθεί το set, δεν αποθηκεύεται ως performed set και δεν μπαίνει στα analytics.

Το rule είναι **both required or the set remains incomplete**. Κενό field δεν σημαίνει `0`, inheritance ή διαγραφή προηγούμενων δεδομένων.

### Exercise tracking modes

Το required-field validation εξαρτάται από το tracking mode της άσκησης:

| Tracking mode | Required inputs | Example | Release |
| --- | --- | --- | --- |
| `Weight × Reps` | Weight + reps | Bench Press | MVP |
| `Bodyweight Reps` | Reps | Push-up / Pull-up | MVP |
| `Bodyweight + Extra Weight` | Extra weight + reps | Weighted Pull-up / Dip | MVP |
| `Assistance × Reps` | Assistance weight + reps | Assisted Pull-up | MVP |
| `Reps only` | Reps | Air Squat | MVP |
| `Duration` | Time | Plank | MVP |
| `Distance + Duration` | Distance + time | Running/rowing | V1 |

Για bodyweight exercises δεν χρησιμοποιούμε και δεν προσθέτουμε το σωματικό βάρος του χρήστη στο set load.

- Unweighted bodyweight set: αποθηκεύονται reps και προαιρετικά RIR.
- Weighted bodyweight set: αποθηκεύονται μόνο `extraWeightKg` και reps.
- Assisted set: αποθηκεύονται `assistanceKg` και reps.
- Bodyweight progress: περισσότερα reps/sets στο ίδιο difficulty.
- Added-weight progress: περισσότερα extra kg για τα ίδια reps ή περισσότερα reps με τα ίδια extra kg.
- Assisted progress: λιγότερη assistance για τα ίδια reps ή περισσότερα reps με την ίδια assistance.
- Bodyweight volume: total reps. Με extra weight, προαιρετικό extra-load volume = `extraWeightKg × reps`.
- Δεν υπολογίζουμε bodyweight-based volume ή e1RM και δεν αποθηκεύουμε τεχνητό `0 kg`.

### Full editability, multi-select and clipboard

Programs, groups, workout variants, exercises, sets, meals, meal items και nutrition days υποστηρίζουν edit, delete και multi-select όπου βγάζει νόημα.

Bulk actions:

- `Copy`: δημιουργεί ανεξάρτητο clone στο paste target.
- `Cut`: μετακινεί τα επιλεγμένα items μόνο μετά από successful paste.
- `Paste`: ελέγχει αν ο target type είναι συμβατός.
- `Duplicate`: άμεσο copy/paste στο ίδιο parent.
- `Delete`: soft-delete ή recoverable removal όπου υπάρχει ιστορικό.
- `Bulk edit`: κοινά fields όπως rest time, set type, meal type ή date.
- `Undo`: εμφανίζεται μετά από move/delete/bulk change.

Παραδείγματα:

- Αντιγραφή πολλών exercises από `Legs #1` σε `Legs #2`.
- Μετακίνηση ολόκληρου workout variant σε άλλο program/group.
- Duplicate ενός πλήρους PPL program.
- Copy μιας nutrition day σε άλλη ημερομηνία.
- Move επιλεγμένων meals από Monday σε Tuesday.

Οι bulk αλλαγές εκτελούνται atomic: ή πετυχαίνουν όλες ή καμία. Δεν επιτρέπεται cut/paste που αφήνει μισό πρόγραμμα ή μισό meal.

### Βασικοί υπολογισμοί

**Volume load**

```text
exerciseVolume = Σ(weightKg × reps) για όλα τα completed working sets
sessionVolume = Σ(exerciseVolume)
```

Το volume συγκρίνεται κυρίως μέσα στην ίδια άσκηση. Δεν είναι δίκαιο να συγκρίνουμε μηχανήματα ή τελείως διαφορετικές κινήσεις σαν να είναι ισοδύναμα.

**Estimated 1RM — Epley**

```text
e1RM = weightKg × (1 + reps / 30)
```

Χρησιμοποιείται κυρίως για sets 1–10 επαναλήψεων και εμφανίζεται ως εκτίμηση, όχι πραγματικό max.

**Workout frequency**

```text
weeklyWorkoutFrequency = completedSessionsInRange / numberOfWeeksInRange
```

Χωρίς fixed plan δεν εμφανίζουμε adherence percentage, missed-workout rate ή planned-vs-completed score. Δείχνουμε completed sessions ανά εβδομάδα/μήνα και rolling weekly average.

**PR types**

- Heaviest weight for exercise.
- Highest e1RM.
- Most reps at a given weight.
- Highest session volume for the exercise.

### Performance comparison rules

Η πρόοδος συνδέεται κυρίως με το **exercise**, όχι μόνο με το workout variant. Αν το Back Squat υπάρχει και στο `Legs #1` και στο `Legs #2`, το app μπορεί να δείχνει συνολικό exercise history, με φίλτρο ανά variant.

Στην active workout ο χρήστης βλέπει για κάθε set:

```text
Target:   8–10 reps
Previous: 90 kg × 8
Today:    [weight] × [reps]
```

Μετά το workout εμφανίζονται κατανοητές συγκρίσεις:

- `+2 reps at the same weight`
- `+5 kg for the same reps`
- `New estimated 1RM`
- `Higher exercise volume`
- `Matched previous best`

Αν βάρος και επαναλήψεις αλλάξουν ταυτόχρονα, χρησιμοποιούμε e1RM ως συμπληρωματική ένδειξη και δείχνουμε πάντα το πραγματικό top set. Δεν χαρακτηρίζουμε κάθε αύξηση volume ως αύξηση δύναμης.

Δεν δημιουργούμε αυθαίρετο συνολικό «fitness score». Δείχνουμε μετρήσεις που ο χρήστης μπορεί να καταλάβει.
