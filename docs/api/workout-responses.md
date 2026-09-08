# Workout API — what the server sends back

A quick reference for eyeballing the data the workout endpoints return. Shapes come
straight from `backend/MySelf.Api/Workouts/WorkoutContracts.cs`; the values below are
illustrative.

## How to see the real thing

- **Browser DevTools → Network tab** while you click around the app. Each call to
  `http://localhost:5242/api/v1/...` shows its JSON response under *Response* / *Preview*.
- **The machine-readable spec:** `http://localhost:5242/openapi/v1.json` (dev only). Every
  endpoint, every schema. Paste it into <https://editor.swagger.io> for a browsable view.
- **`backend/MySelf.Api/MySelf.Api.http`** — ready-made requests. Open in VS / VS Code with
  the REST Client extension, hit *Send Request*, read the response inline.

All routes are under `/api/v1`, need `Authorization: Bearer <token>`, and return
[RFC 7807 problem+json](https://datatracker.ietf.org/doc/html/rfc7807) on error:
`{ "title": "...", "detail": "...", "status": 400 }` or, for field validation,
`{ "title": "...", "errors": { "field": ["message"] } }`.

---

## Programs

### `GET /programs` · `GET /programs/archived` → `ProgramListItem[]`

```json
[
  {
    "id": "0b8f…",
    "name": "PPL",
    "splitLabel": "Push / Pull / Legs",
    "isActive": true,
    "dayCount": 3,
    "exerciseCount": 18,
    "createdAt": "2026-08-20T09:12:00+00:00"
  }
]
```

`/archived` returns the same shape with `isActive` always `false`.

### `GET /programs/{id}` → `ProgramDetail`

```json
{
  "id": "0b8f…",
  "name": "PPL",
  "splitLabel": "Push / Pull / Legs",
  "isActive": true,
  "createdAt": "2026-08-20T09:12:00+00:00",
  "rowVersion": 41,
  "days": [
    { "id": "d1…", "name": "Push", "sortOrder": 0, "exerciseCount": 6 },
    { "id": "d2…", "name": "Pull", "sortOrder": 1, "exerciseCount": 6 }
  ]
}
```

`rowVersion` is the Postgres `xmin` concurrency token — send it back on `PUT /programs/{id}`
and `PUT /workout-days/{id}`; a stale value gets a `409`.

### `GET /programs/{id}/stats` → `ProgramStats`  *(new — the Overview tab)*

```json
{
  "totalSessions": 12,
  "firstPerformedOn": "2026-08-01",
  "lastPerformedOn": "2026-09-04",
  "sessionsThisWeek": 2,
  "sessionsThisMonth": 3,
  "weeklyAverage": 2.5,
  "totalVolumeKg": 48250.00,
  "avgDurationSeconds": 3540,
  "completedSets": 168,
  "skippedSets": 9,
  "skippedSetRate": 0.051,
  "perDay": [
    { "dayId": "d1…", "dayName": "Push", "sessions": 5, "lastPerformedOn": "2026-09-04" },
    { "dayId": "d2…", "dayName": "Pull", "sessions": 4, "lastPerformedOn": "2026-09-02" },
    { "dayId": "d3…", "dayName": "Legs", "sessions": 3, "lastPerformedOn": "2026-08-30" }
  ],
  "personalRecords": [
    { "exerciseName": "Back Squat", "type": "HeaviestWeight", "value": 140.0, "achievedOn": "2026-09-04" }
  ]
}
```

- Only counts **completed** sessions whose `SourceProgramId` is this program (set when the
  session starts; existing sessions were backfilled from their source day).
- `weeklyAverage` = completed sessions ÷ weeks the program has existed, capped at 8 weeks.
  No adherence % / missed-workout rate — docs/02 forbids those without a fixed plan.
- `skippedSetRate` is `skipped / (completed + skipped)`, `0`–`1`.
- Optional `?today=YYYY-MM-DD` — the caller's local date; defaults to the server's UTC date.
- Empty program: `totalSessions: 0`, every other number `0`/`null`, `perDay` still lists the
  days at zero.

---

## Days

### `GET /workout-days/{id}` → `DayDetail`

```json
{
  "id": "d1…",
  "name": "Push",
  "sortOrder": 0,
  "estimatedDurationMinutes": 60,
  "programRowVersion": 41,
  "exercises": [
    {
      "id": "de1…",
      "exerciseId": "ex-bench…",
      "exerciseName": "Barbell Bench Press",
      "sortOrder": 0,
      "supersetGroupId": null,
      "supersetMemberOrder": 0,
      "restSeconds": 150,
      "notes": null,
      "sets": [
        {
          "id": "sp1…", "sortOrder": 0, "kind": "Standard", "isAmrap": false,
          "targetToFailure": false, "targetRepsMin": 5, "targetRepsMax": 8,
          "targetWeightKg": 80.0, "targetRir": 2
        }
      ]
    }
  ],
  "supersets": [
    { "id": "ss1…", "sortOrder": 0, "restAfterRoundSeconds": 90 }
  ]
}
```

---

## Running a session

### `POST /workout-sessions` · `GET /workout-sessions/{id}` · `GET /workout-sessions/active` · `POST …/complete` → `WorkoutSessionDetail`

```json
{
  "id": "s1…",
  "sourceDayId": "d1…",
  "dayName": "Push",
  "programName": "PPL",
  "status": "InProgress",
  "startedAt": "2026-09-05T17:41:03+00:00",
  "completedAt": null,
  "performedOnLocalDate": null,
  "notes": null,
  "summary": {
    "durationSeconds": null,
    "completedSetCount": 0,
    "skippedSetCount": 0,
    "totalReps": 0,
    "totalVolumeKg": 0
  },
  "newPersonalRecords": [],
  "exercises": [
    {
      "id": "el1…",
      "exerciseId": "ex-bench…",
      "exerciseName": "Barbell Bench Press",
      "trackingMode": "WeightAndReps",
      "sortOrder": 0,
      "restSeconds": 150,
      "supersetGroupSnapshotId": null,
      "supersetMemberOrder": 0,
      "supersetRestAfterRoundSeconds": null,
      "sets": [
        {
          "id": "sl1…", "sortOrder": 0, "kind": "Standard", "isAmrap": false,
          "targetToFailure": false, "targetRepsMin": 5, "targetRepsMax": 8,
          "targetWeightKg": 80.0, "targetRir": 2,
          "weightKg": null, "addedWeightKg": null, "assistanceKg": null,
          "reps": null, "durationSeconds": null, "distanceMeters": null,
          "rir": null, "reachedFailure": false,
          "completedAt": null, "skippedAt": null, "skippedReason": null
        }
      ]
    }
  ]
}
```

- Everything under `exercises` is a **snapshot taken at start** — later edits to the program
  never rewrite a session.
- `status`: `InProgress` | `Completed` | `Discarded`.
- On `POST …/complete`, `status` becomes `Completed`, `performedOnLocalDate` is stamped, the
  `summary` fills in, and `newPersonalRecords` lists any PRs set this session. Completing a
  session with **nothing logged or skipped** is rejected with `400 { errors: { session: […] } }`.
- `POST …/set-logs` and `POST …/skip-set` return just the one changed set (`SetLogDetail`).

### `GET /workout-sessions?status=Completed&page=1&pageSize=20` → `WorkoutSessionListResult`

```json
{
  "items": [
    {
      "id": "s0…", "dayName": "Legs", "programName": "PPL", "status": "Completed",
      "startedAt": "2026-09-04T07:02:00+00:00", "completedAt": "2026-09-04T08:05:00+00:00",
      "performedOnLocalDate": "2026-09-04",
      "summary": { "durationSeconds": 3780, "completedSetCount": 15, "skippedSetCount": 1, "totalReps": 132, "totalVolumeKg": 9450 }
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 12
}
```

### `GET /workout-calendar?from=2026-09-01&to=2026-09-30[&programId=…]` → `WorkoutCalendarResult`

```json
{
  "from": "2026-09-01",
  "to": "2026-09-30",
  "days": [
    { "date": "2026-09-02", "sessions": [ /* WorkoutSessionListItem[] */ ] },
    { "date": "2026-09-04", "sessions": [ /* … */ ] }
  ]
}
```

`?programId=` (new) limits it to sessions started from that program. Only days with at least
one completed session appear.

---

## Progress / analytics

### `GET /exercises/{id}/history[?dayId=…]` → `ExerciseHistoryResult`

```json
{
  "exerciseId": "ex-squat…",
  "exerciseName": "Back Squat",
  "personalRecords": [
    { "type": "BestEstimatedOneRepMax", "value": 172.3, "weightKg": 150.0, "reps": 5, "achievedOn": "2026-09-04" }
  ],
  "sessions": [
    {
      "sessionId": "s0…", "performedOn": "2026-09-04", "dayName": "Legs",
      "topSetWeightKg": 150.0, "topSetReps": 5,
      "estimatedOneRepMax": 172.3, "volume": 3750.0, "completedSets": 4
    }
  ]
}
```

### `GET /analytics/strength?exerciseId=…&range=30d|90d|1y|all` → `StrengthAnalyticsResult`

```json
{
  "exerciseId": "ex-squat…",
  "range": "90d",
  "points": [
    { "date": "2026-07-10", "estimatedOneRepMax": 160.0, "volume": 3200.0 },
    { "date": "2026-08-04", "estimatedOneRepMax": 166.7, "volume": 3500.0 }
  ]
}
```

---

## Exercise catalogue

### `GET /exercises?q=squat&pageSize=25` → `ExerciseSearchResult`

```json
{
  "items": [
    {
      "id": "ex-squat…",
      "name": "Barbell Full Squat",
      "category": "Legs",
      "defaultTrackingMode": "WeightAndReps",
      "primaryMuscles": ["Quads"],
      "secondaryMuscles": ["Glutes", "Hamstrings"],
      "equipment": ["Barbell"],
      "imageThumbUrl": "https://wger.de/media/exercise-images/1801/….200x200_q85.jpg",
      "imageUrl": "https://wger.de/media/exercise-images/1801/….jpg",
      "imageAttribution": "Workout Guru · wger.de (CC BY-SA)"
    }
  ],
  "page": 1,
  "pageSize": 25,
  "total": 1
}
```

- `defaultTrackingMode`: `WeightAndReps` | `BodyweightReps` | `BodyweightPlusWeight` |
  `AssistanceReps` | `Duration` | `RepsOnly` — decides which performed fields a set shows.
- `primaryMuscles` / `secondaryMuscles`: muscle-group names (85% of the catalogue has them);
  `equipment`: e.g. `Barbell`, `Dumbbell`, `Cable machine`, `none (bodyweight exercise)`.
- `image*`: a hot-linked wger CC-BY-SA illustration — **null for ~73% of exercises**. Show
  `imageThumbUrl` (200px) in lists, `imageUrl` full-size; render `imageAttribution` near it.
- `GET /exercises/{id}` returns the **same object shape** (one item).

### `GET /muscles` → `MuscleGroup[]`

```json
[ { "id": 4, "name": "Chest", "isFront": true }, { "id": 10, "name": "Quads", "isFront": true } ]
```

The 15 muscle groups, for the day-focus picker. `WorkoutDay.focusMuscleIds` on `GET
/workout-days/{id}` is a list of these ids; `PUT` accepts it (unknown ids are dropped).
