import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, apiFetch } from "../../lib/api";
import { useAuth } from "../auth/auth";
import { enqueue } from "./offlineQueue";

/**
 * Run a set mutation; if it fails with a network (non-ApiError) error, queue it for retry
 * and resolve so the workout isn't blocked (docs/02 offline autosave). A real rejection
 * (validation / 404 / 409) still throws.
 */
async function postOrQueue<T>(url: string, body: unknown, accessToken: string | undefined): Promise<T | null> {
  try {
    return await apiFetch<T>(url, { method: "POST", body, accessToken });
  } catch (e) {
    if (e instanceof ApiError) throw e;
    enqueue(url, body);
    return null;
  }
}

// --- types (hand-written until the OpenAPI client lands) ---

export type ExerciseListItem = {
  id: string;
  name: string;
  category: string;
  defaultTrackingMode: string;
  primaryMuscles: string[];
  secondaryMuscles: string[];
  equipment: string[];
  imageThumbUrl: string | null;
  imageUrl: string | null;
  imageAttribution: string | null;
};

export type ExerciseSearchResult = {
  items: ExerciseListItem[];
  page: number;
  pageSize: number;
  total: number;
};

export type ProgramListItem = {
  id: string;
  name: string;
  splitLabel: string | null;
  isActive: boolean;
  dayCount: number;
  exerciseCount: number;
  createdAt: string;
};

export type DayListItem = { id: string; name: string; sortOrder: number; exerciseCount: number };
export type ProgramDetail = {
  id: string;
  name: string;
  splitLabel: string | null;
  isActive: boolean;
  createdAt: string;
  // xmin concurrency token — echo back on PUT /programs and PUT /workout-days.
  rowVersion: number;
  days: DayListItem[];
};

export type ProgramDayStat = {
  dayId: string;
  dayName: string;
  sessions: number;
  lastPerformedOn: string | null;
};

export type ProgramPrStat = {
  exerciseName: string;
  type: string;
  value: number;
  achievedOn: string;
};

export type MuscleWeeklySets = { muscle: string; setsPerWeek: number };

/** Aggregates for a program's Overview tab (GET /api/v1/programs/{id}/stats). */
export type ProgramStats = {
  totalSessions: number;
  firstPerformedOn: string | null;
  lastPerformedOn: string | null;
  sessionsThisWeek: number;
  sessionsThisMonth: number;
  weeklyAverage: number;
  totalVolumeKg: number;
  avgDurationSeconds: number | null;
  completedSets: number;
  skippedSets: number;
  skippedSetRate: number;
  perDay: ProgramDayStat[];
  personalRecords: ProgramPrStat[];
  muscleWeeklySets: MuscleWeeklySets[];
};

export type SetPrescriptionDetail = {
  id: string;
  sortOrder: number;
  kind: "Standard" | "Drop";
  isAmrap: boolean;
  targetToFailure: boolean;
  targetRepsMin: number | null;
  targetRepsMax: number | null;
  targetWeightKg: number | null;
  targetRir: number | null;
};

export type DayExerciseDetail = {
  id: string;
  exerciseId: string;
  exerciseName: string;
  sortOrder: number;
  supersetGroupId: string | null;
  supersetMemberOrder: number;
  restSeconds: number | null;
  notes: string | null;
  sets: SetPrescriptionDetail[];
};

export type DayDetail = {
  id: string;
  name: string;
  sortOrder: number;
  estimatedDurationMinutes: number | null;
  // The owning program's xmin token; send it back on PUT to guard the edit.
  programRowVersion: number;
  exercises: DayExerciseDetail[];
  supersets: { id: string; sortOrder: number; restAfterRoundSeconds: number }[];
  // Muscle-group ids this day trains (empty = no focus set).
  focusMuscleIds: number[];
};

export type MuscleGroup = { id: number; name: string; isFront: boolean };

export type UpdateDayExercise = {
  exerciseId: string;
  sortOrder: number;
  supersetRef: string | null;
  supersetMemberOrder: number;
  restSeconds: number | null;
  notes: string | null;
  sets: {
    sortOrder: number;
    kind: "Standard" | "Drop";
    isAmrap: boolean;
    targetToFailure: boolean;
    targetRepsMin: number | null;
    targetRepsMax: number | null;
    targetWeightKg: number | null;
    targetRir: number | null;
  }[];
};

export type UpdateDayBody = {
  name?: string;
  estimatedDurationMinutes?: number | null;
  // The program xmin token from the last read; omit to accept last-write-wins.
  rowVersion?: number;
  exercises: UpdateDayExercise[];
  supersets: { ref: string; sortOrder: number; restAfterRoundSeconds: number }[];
  // Muscle-group ids the day focuses on. Omit = leave as-is; [] = clear.
  focusMuscleIds?: number[];
};

// --- workout sessions (docs/02 "Starting a workout", Story 3A) ---

export type SetLogDetail = {
  id: string;
  sortOrder: number;
  kind: "Standard" | "Drop";
  isAmrap: boolean;
  targetToFailure: boolean;
  targetRepsMin: number | null;
  targetRepsMax: number | null;
  targetWeightKg: number | null;
  targetRir: number | null;
  weightKg: number | null;
  addedWeightKg: number | null;
  assistanceKg: number | null;
  reps: number | null;
  durationSeconds: number | null;
  distanceMeters: number | null;
  rir: number | null;
  reachedFailure: boolean;
  completedAt: string | null;
  skippedAt: string | null;
  skippedReason: string | null;
};

export type ExerciseLogDetail = {
  id: string;
  exerciseId: string;
  exerciseName: string;
  trackingMode: string;
  sortOrder: number;
  restSeconds: number | null;
  supersetGroupSnapshotId: string | null;
  supersetMemberOrder: number;
  supersetRestAfterRoundSeconds: number | null;
  sets: SetLogDetail[];
};

export type SessionSummary = {
  durationSeconds: number | null;
  completedSetCount: number;
  skippedSetCount: number;
  totalReps: number;
  totalVolumeKg: number;
};

export type PersonalRecordType =
  | "HeaviestWeight"
  | "BestEstimatedOneRepMax"
  | "MostRepsAtWeight"
  | "BestExerciseVolume";

export type PersonalRecordDetail = {
  type: PersonalRecordType;
  value: number;
  weightKg: number | null;
  reps: number | null;
  achievedOn: string;
};

export type WorkoutSessionDetail = {
  id: string;
  sourceDayId: string | null;
  dayName: string | null;
  programName: string | null;
  status: "InProgress" | "Completed" | "Discarded";
  startedAt: string;
  completedAt: string | null;
  performedOnLocalDate: string | null;
  notes: string | null;
  wasEdited: boolean;
  summary: SessionSummary;
  newPersonalRecords: PersonalRecordDetail[];
  exercises: ExerciseLogDetail[];
};

export type ExerciseHistoryEntry = {
  sessionId: string;
  performedOn: string;
  dayName: string | null;
  topSetWeightKg: number | null;
  topSetReps: number | null;
  estimatedOneRepMax: number | null;
  volume: number;
  completedSets: number;
};

export type ExerciseHistoryResult = {
  exerciseId: string;
  exerciseName: string;
  personalRecords: PersonalRecordDetail[];
  sessions: ExerciseHistoryEntry[];
};

export type StrengthPoint = { date: string; estimatedOneRepMax: number | null; volume: number };
export type StrengthAnalyticsResult = { exerciseId: string; range: string; points: StrengthPoint[] };

export type WorkoutSessionListItem = {
  id: string;
  dayName: string | null;
  programName: string | null;
  status: "InProgress" | "Completed" | "Discarded";
  startedAt: string;
  completedAt: string | null;
  performedOnLocalDate: string | null;
  wasEdited: boolean;
  summary: SessionSummary;
};

export type WorkoutSessionListResult = {
  items: WorkoutSessionListItem[];
  page: number;
  pageSize: number;
  total: number;
};

export type CalendarDay = { date: string; sessions: WorkoutSessionListItem[] };
export type WorkoutCalendarResult = { from: string; to: string; days: CalendarDay[] };

export type LogSetBody = {
  setLogId: string;
  weightKg?: number | null;
  addedWeightKg?: number | null;
  assistanceKg?: number | null;
  reps?: number | null;
  durationSeconds?: number | null;
  distanceMeters?: number | null;
  rir?: number | null;
  reachedFailure: boolean;
};

// --- hooks ---

function useToken() {
  return useAuth().session?.accessToken;
}

export function usePrograms() {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["programs"],
    queryFn: () => apiFetch<ProgramListItem[]>("/api/v1/programs", { accessToken }),
    enabled: accessToken != null,
  });
}

export function useArchivedPrograms(enabled: boolean) {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["programs", "archived"],
    queryFn: () => apiFetch<ProgramListItem[]>("/api/v1/programs/archived", { accessToken }),
    enabled: accessToken != null && enabled,
  });
}

export function useProgram(id: string | null) {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["program", id],
    queryFn: () => apiFetch<ProgramDetail>(`/api/v1/programs/${id}`, { accessToken }),
    enabled: accessToken != null && id != null,
  });
}

export function useDay(id: string | null) {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["day", id],
    queryFn: () => apiFetch<DayDetail>(`/api/v1/workout-days/${id}`, { accessToken }),
    enabled: accessToken != null && id != null,
  });
}

export function useExerciseSearch(q: string) {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["exercises", q],
    queryFn: () =>
      apiFetch<ExerciseSearchResult>(
        `/api/v1/exercises?pageSize=25${q ? `&q=${encodeURIComponent(q)}` : ""}`,
        { accessToken },
      ),
    enabled: accessToken != null,
  });
}

/** The catalogue's ~15 muscle groups — for the day-focus picker. Effectively static. */
export function useMuscles() {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["muscles"],
    queryFn: () => apiFetch<MuscleGroup[]>("/api/v1/muscles", { accessToken }),
    enabled: accessToken != null,
    staleTime: 60 * 60 * 1000,
  });
}

/** Flip a small UI preference (PUT /api/v1/me/preferences); refreshes GET /me. */
export function useUpdatePreferences() {
  const accessToken = useToken();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { warnOffFocusExercises: boolean }) =>
      apiFetch<unknown>("/api/v1/me/preferences", { method: "PUT", body, accessToken }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["me"] }),
  });
}

export function useCreateProgram() {
  const accessToken = useToken();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; splitLabel?: string }) =>
      apiFetch<ProgramListItem>("/api/v1/programs", { method: "POST", body, accessToken }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["programs"] }),
  });
}

export function useMutateProgram(programId: string | null) {
  const accessToken = useToken();
  const qc = useQueryClient();
  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ["programs"] });
    qc.invalidateQueries({ queryKey: ["program", programId] });
  };

  return {
    activate: useMutation({
      mutationFn: (id: string) =>
        apiFetch<void>(`/api/v1/programs/${id}/activate`, { method: "POST", accessToken }),
      onSuccess: invalidate,
    }),
    remove: useMutation({
      mutationFn: (id: string) =>
        apiFetch<void>(`/api/v1/programs/${id}`, { method: "DELETE", accessToken }),
      onSuccess: invalidate,
    }),
    archive: useMutation({
      mutationFn: (id: string) =>
        apiFetch<void>(`/api/v1/programs/${id}/archive`, { method: "POST", accessToken }),
      onSuccess: invalidate,
    }),
    clone: useMutation({
      mutationFn: (id: string) =>
        apiFetch<ProgramListItem>(`/api/v1/programs/${id}/clone`, { method: "POST", accessToken }),
      onSuccess: invalidate,
    }),
    restore: useMutation({
      mutationFn: (id: string) =>
        apiFetch<void>(`/api/v1/programs/${id}/restore`, { method: "POST", accessToken }),
      onSuccess: invalidate,
    }),
    addDay: useMutation({
      mutationFn: (body: { name: string }) =>
        apiFetch<DayListItem>(`/api/v1/programs/${programId}/days`, { method: "POST", body, accessToken }),
      onSuccess: invalidate,
    }),
    deleteDay: useMutation({
      mutationFn: (dayId: string) =>
        apiFetch<void>(`/api/v1/workout-days/${dayId}`, { method: "DELETE", accessToken }),
      onSuccess: invalidate,
    }),
    // Rename / relabel a program and/or reorder its days. Pass the program's rowVersion.
    updateProgram: useMutation({
      mutationFn: (body: { name?: string; splitLabel?: string | null; dayOrder?: string[]; rowVersion: number }) =>
        apiFetch<void>(`/api/v1/programs/${programId}`, { method: "PUT", body, accessToken }),
      onSuccess: invalidate,
    }),
  };
}

export function useUpdateDay(programId: string | null) {
  const accessToken = useToken();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ dayId, body }: { dayId: string; body: UpdateDayBody }) =>
      apiFetch<DayDetail>(`/api/v1/workout-days/${dayId}`, {
        method: "PUT",
        body,
        accessToken,
      }),
    onSuccess: (_data, { dayId }) => {
      qc.invalidateQueries({ queryKey: ["day", dayId] });
      qc.invalidateQueries({ queryKey: ["program", programId] });
      qc.invalidateQueries({ queryKey: ["programs"] });
    },
  });
}

export type BulkExercisesArgs = {
  destDayId: string;
  sourceDayId: string;
  dayExerciseIds: string[];
  rowVersion?: number;
};

/** Copy / move selected exercises between two of the caller's days (docs/08 Story 7). */
export function useBulkExercises(programId: string | null) {
  const accessToken = useToken();
  const qc = useQueryClient();
  const invalidate = (destDayId: string, sourceDayId: string) => {
    qc.invalidateQueries({ queryKey: ["day", destDayId] });
    qc.invalidateQueries({ queryKey: ["day", sourceDayId] });
    qc.invalidateQueries({ queryKey: ["program", programId] });
    qc.invalidateQueries({ queryKey: ["programs"] });
  };

  const call = (kind: "copy" | "move") => (args: BulkExercisesArgs) =>
    apiFetch<DayDetail>(`/api/v1/workout-days/${args.destDayId}/exercises/bulk-${kind}`, {
      method: "POST",
      body: {
        sourceDayId: args.sourceDayId,
        dayExerciseIds: args.dayExerciseIds,
        rowVersion: args.rowVersion,
      },
      accessToken,
    });

  return {
    copy: useMutation({
      mutationFn: call("copy"),
      onSuccess: (_d, a) => invalidate(a.destDayId, a.sourceDayId),
    }),
    move: useMutation({
      mutationFn: call("move"),
      onSuccess: (_d, a) => invalidate(a.destDayId, a.sourceDayId),
    }),
  };
}

const ACTIVE_CACHE_KEY = "myself.activeSession";

function readActiveCache(): WorkoutSessionDetail | null | undefined {
  try {
    const raw = localStorage.getItem(ACTIVE_CACHE_KEY);
    return raw ? (JSON.parse(raw) as WorkoutSessionDetail) : undefined;
  } catch {
    return undefined;
  }
}

/**
 * The caller's InProgress session, or null when nothing is running (a 404 isn't an error).
 * The last result is mirrored to localStorage so a mid-workout reload paints instantly
 * before the network responds (docs/02 "browser refresh/crash restores the draft").
 */
export function useActiveSession() {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["workout-session", "active"],
    queryFn: async () => {
      let result: WorkoutSessionDetail | null;
      try {
        result = await apiFetch<WorkoutSessionDetail>("/api/v1/workout-sessions/active", { accessToken });
      } catch (e) {
        if (e instanceof ApiError && e.status === 404) {
          result = null;
        } else {
          throw e;
        }
      }
      try {
        if (result) localStorage.setItem(ACTIVE_CACHE_KEY, JSON.stringify(result));
        else localStorage.removeItem(ACTIVE_CACHE_KEY);
      } catch {
        /* ignore storage failures */
      }
      return result;
    },
    initialData: readActiveCache,
    enabled: accessToken != null,
  });
}

/** Start a session from a day, or pass null for an ad-hoc session (docs/02 Story 3A). */
export function useStartSession() {
  const accessToken = useToken();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dayId: string | null) =>
      apiFetch<WorkoutSessionDetail>("/api/v1/workout-sessions", { method: "POST", body: { dayId }, accessToken }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["workout-session"] }),
  });
}

/** Per-exercise history: current PRs + one entry per completed session it appears in. */
export function useExerciseHistory(exerciseId: string | null, dayId?: string) {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["exercise-history", exerciseId, dayId ?? null],
    queryFn: () =>
      apiFetch<ExerciseHistoryResult>(
        `/api/v1/exercises/${exerciseId}/history${dayId ? `?dayId=${dayId}` : ""}`,
        { accessToken },
      ),
    enabled: accessToken != null && exerciseId != null,
  });
}

/** e1RM + volume trend for one exercise over a range ("30d" | "90d" | "1y" | "all"). */
export function useStrengthAnalytics(exerciseId: string | null, range: string) {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["strength-analytics", exerciseId, range],
    queryFn: () =>
      apiFetch<StrengthAnalyticsResult>(
        `/api/v1/analytics/strength?exerciseId=${exerciseId}&range=${range}`,
        { accessToken },
      ),
    enabled: accessToken != null && exerciseId != null,
  });
}

/** Completed sessions between two ISO dates, grouped by local performed-date.
 *  Pass a programId to limit it to sessions started from that program. */
export function useWorkoutCalendar(from: string, to: string, programId?: string) {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["workout-calendar", from, to, programId ?? null],
    queryFn: () =>
      apiFetch<WorkoutCalendarResult>(
        `/api/v1/workout-calendar?from=${from}&to=${to}${programId ? `&programId=${programId}` : ""}`,
        { accessToken },
      ),
    enabled: accessToken != null,
  });
}

/** Aggregate numbers for a program's Overview tab. */
export function useProgramStats(programId: string | null) {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["program-stats", programId],
    queryFn: () => apiFetch<ProgramStats>(`/api/v1/programs/${programId}/stats`, { accessToken }),
    enabled: accessToken != null && programId != null,
  });
}

/** Correct the local date a completed session counts against. */
export function useRescheduleSession() {
  const accessToken = useToken();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ sessionId, localDate }: { sessionId: string; localDate: string }) =>
      apiFetch<WorkoutSessionDetail>(`/api/v1/workout-sessions/${sessionId}/reschedule`, {
        method: "POST",
        body: { localDate },
        accessToken,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["workout-calendar"] });
      qc.invalidateQueries({ queryKey: ["workout-session"] });
      qc.invalidateQueries({ queryKey: ["program-stats"] });
    },
  });
}

/** One session by id — used by the completed-workout edit screen. */
export function useSession(id: string | null) {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["workout-session", id],
    queryFn: () => apiFetch<WorkoutSessionDetail>(`/api/v1/workout-sessions/${id}`, { accessToken }),
    enabled: accessToken != null && id != null,
  });
}

/** Past workouts, newest first (default status Completed). */
export function useSessionHistory(status: "Completed" | "Discarded" = "Completed") {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["workout-session", "history", status],
    queryFn: () =>
      apiFetch<WorkoutSessionListResult>(`/api/v1/workout-sessions?status=${status}&pageSize=50`, { accessToken }),
    enabled: accessToken != null,
  });
}

/** Everything a set change can move: the session itself, plus (when editing a completed
 *  one) the history list, calendar, program stats and per-exercise strength views. */
function invalidateAfterSetChange(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: ["workout-session"] });
  qc.invalidateQueries({ queryKey: ["workout-calendar"] });
  qc.invalidateQueries({ queryKey: ["program-stats"] });
  qc.invalidateQueries({ queryKey: ["exercise-history"] });
  qc.invalidateQueries({ queryKey: ["strength-analytics"] });
}

/** Log (or re-log) a performed set — validated server-side by the exercise's tracking mode. */
export function useLogSet(sessionId: string) {
  const accessToken = useToken();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: LogSetBody) =>
      postOrQueue<SetLogDetail>(`/api/v1/workout-sessions/${sessionId}/set-logs`, body, accessToken),
    onSuccess: () => invalidateAfterSetChange(qc),
  });
}

/** Explicitly skip a set (docs/07). Reason is optional. */
export function useSkipSet(sessionId: string) {
  const accessToken = useToken();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ setLogId, reason }: { setLogId: string; reason?: string }) =>
      postOrQueue<SetLogDetail>(
        `/api/v1/workout-sessions/${sessionId}/skip-set`,
        { setLogId, reason: reason ?? null },
        accessToken,
      ),
    onSuccess: () => invalidateAfterSetChange(qc),
  });
}

/** Finish the session. The client sends its own local calendar date (locked decision #8). */
export function useCompleteSession(sessionId: string) {
  const accessToken = useToken();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (notes?: string) => {
      const now = new Date();
      const localDate = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
      return apiFetch<WorkoutSessionDetail>(`/api/v1/workout-sessions/${sessionId}/complete`, {
        method: "POST",
        body: { localDate, notes: notes ?? null },
        accessToken,
      });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["workout-session"] });
      qc.invalidateQueries({ queryKey: ["program-stats"] });
      qc.invalidateQueries({ queryKey: ["workout-calendar"] });
    },
  });
}

/** Throw the in-progress session away without recording it. */
export function useDiscardSession(sessionId: string) {
  const accessToken = useToken();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () =>
      apiFetch<void>(`/api/v1/workout-sessions/${sessionId}/discard`, { method: "POST", body: {}, accessToken }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["workout-session"] }),
  });
}

/** Mid-session structure edits: add / replace / add-set / remove an exercise (docs/02 §7). */
export function useSessionExercises(sessionId: string) {
  const accessToken = useToken();
  const qc = useQueryClient();
  const invalidate = () => qc.invalidateQueries({ queryKey: ["workout-session"] });
  const base = `/api/v1/workout-sessions/${sessionId}`;

  return {
    add: useMutation({
      mutationFn: (exerciseId: string) =>
        apiFetch<WorkoutSessionDetail>(`${base}/exercises`, { method: "POST", body: { exerciseId, sets: 3 }, accessToken }),
      onSuccess: invalidate,
    }),
    replace: useMutation({
      mutationFn: ({ exerciseLogId, exerciseId, scope }: { exerciseLogId: string; exerciseId: string; scope: "TodayOnly" | "TodayAndFuture" }) =>
        apiFetch<WorkoutSessionDetail>(`${base}/exercises/${exerciseLogId}/replace`, {
          method: "POST",
          body: { exerciseId, scope },
          accessToken,
        }),
      onSuccess: invalidate,
    }),
    addSet: useMutation({
      mutationFn: (exerciseLogId: string) =>
        apiFetch<WorkoutSessionDetail>(`${base}/exercises/${exerciseLogId}/add-set`, { method: "POST", body: {}, accessToken }),
      onSuccess: invalidate,
    }),
    remove: useMutation({
      mutationFn: (exerciseLogId: string) =>
        apiFetch<WorkoutSessionDetail>(`${base}/exercises/${exerciseLogId}`, { method: "DELETE", accessToken }),
      onSuccess: invalidate,
    }),
  };
}
