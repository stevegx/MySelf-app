import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../lib/api";
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
  groupCount: number;
  variantCount: number;
  createdAt: string;
};

export type VariantListItem = { id: string; name: string; sortOrder: number; exerciseCount: number };
export type GroupDetail = { id: string; name: string; sortOrder: number; variants: VariantListItem[] };
export type ProgramDetail = {
  id: string;
  name: string;
  splitLabel: string | null;
  isActive: boolean;
  createdAt: string;
  // xmin concurrency token — echo back on PUT /programs and PUT /workout-variants.
  rowVersion: number;
  groups: GroupDetail[];
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

export type VariantExerciseDetail = {
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

export type VariantDetail = {
  id: string;
  name: string;
  sortOrder: number;
  estimatedDurationMinutes: number | null;
  // The owning program's xmin token; send it back on PUT to guard the edit.
  programRowVersion: number;
  exercises: VariantExerciseDetail[];
  supersets: { id: string; sortOrder: number; restAfterRoundSeconds: number }[];
};

export type UpdateVariantExercise = {
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

export type UpdateVariantBody = {
  name?: string;
  estimatedDurationMinutes?: number | null;
  // The program xmin token from the last read; omit to accept last-write-wins.
  rowVersion?: number;
  exercises: UpdateVariantExercise[];
  supersets: { ref: string; sortOrder: number; restAfterRoundSeconds: number }[];
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

export function useProgram(id: string | null) {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["program", id],
    queryFn: () => apiFetch<ProgramDetail>(`/api/v1/programs/${id}`, { accessToken }),
    enabled: accessToken != null && id != null,
  });
}

export function useVariant(id: string | null) {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["variant", id],
    queryFn: () => apiFetch<VariantDetail>(`/api/v1/workout-variants/${id}`, { accessToken }),
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
    addGroup: useMutation({
      mutationFn: (body: { name: string }) =>
        apiFetch<GroupDetail>(`/api/v1/programs/${programId}/groups`, { method: "POST", body, accessToken }),
      onSuccess: invalidate,
    }),
    addVariant: useMutation({
      mutationFn: ({ groupId, name }: { groupId: string; name: string }) =>
        apiFetch<VariantListItem>(`/api/v1/workout-groups/${groupId}/variants`, {
          method: "POST",
          body: { name },
          accessToken,
        }),
      onSuccess: invalidate,
    }),
    deleteGroup: useMutation({
      mutationFn: (groupId: string) =>
        apiFetch<void>(`/api/v1/workout-groups/${groupId}`, { method: "DELETE", accessToken }),
      onSuccess: invalidate,
    }),
    deleteVariant: useMutation({
      mutationFn: (variantId: string) =>
        apiFetch<void>(`/api/v1/workout-variants/${variantId}`, { method: "DELETE", accessToken }),
      onSuccess: invalidate,
    }),
  };
}

export function useUpdateVariant(programId: string | null) {
  const accessToken = useToken();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ variantId, body }: { variantId: string; body: UpdateVariantBody }) =>
      apiFetch<VariantDetail>(`/api/v1/workout-variants/${variantId}`, {
        method: "PUT",
        body,
        accessToken,
      }),
    onSuccess: (_data, { variantId }) => {
      qc.invalidateQueries({ queryKey: ["variant", variantId] });
      qc.invalidateQueries({ queryKey: ["program", programId] });
      qc.invalidateQueries({ queryKey: ["programs"] });
    },
  });
}
