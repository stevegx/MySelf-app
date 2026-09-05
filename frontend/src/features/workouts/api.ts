import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, apiFetch } from "../../lib/api";
import { useAuth } from "../auth/auth";

// --- types (hand-written until the OpenAPI client lands) ---

export type ExerciseListItem = {
  id: string;
  name: string;
  category: string;
  defaultTrackingMode: string;
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
};

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
  supersetGroupSnapshotId: string | null;
  supersetMemberOrder: number;
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
  summary: SessionSummary;
};

export type WorkoutSessionListResult = {
  items: WorkoutSessionListItem[];
  page: number;
  pageSize: number;
  total: number;
};

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
    archive: useMutation({
      mutationFn: (id: string) =>
        apiFetch<void>(`/api/v1/programs/${id}`, { method: "DELETE", accessToken }),
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

/** The caller's InProgress session, or null when nothing is running — a 404 isn't an error here. */
export function useActiveSession() {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["workout-session", "active"],
    queryFn: async () => {
      try {
        return await apiFetch<WorkoutSessionDetail>("/api/v1/workout-sessions/active", { accessToken });
      } catch (e) {
        if (e instanceof ApiError && e.status === 404) return null;
        throw e;
      }
    },
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

/** Log (or re-log) a performed set — validated server-side by the exercise's tracking mode. */
export function useLogSet(sessionId: string) {
  const accessToken = useToken();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: LogSetBody) =>
      apiFetch<SetLogDetail>(`/api/v1/workout-sessions/${sessionId}/set-logs`, { method: "POST", body, accessToken }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["workout-session"] }),
  });
}

/** Explicitly skip a set (docs/07). Reason is optional. */
export function useSkipSet(sessionId: string) {
  const accessToken = useToken();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ setLogId, reason }: { setLogId: string; reason?: string }) =>
      apiFetch<SetLogDetail>(`/api/v1/workout-sessions/${sessionId}/skip-set`, {
        method: "POST",
        body: { setLogId, reason: reason ?? null },
        accessToken,
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["workout-session"] }),
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
    onSuccess: () => qc.invalidateQueries({ queryKey: ["workout-session"] }),
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
