import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../lib/api";
import { useAuth } from "../auth/auth";

function useToken() {
  return useAuth().session?.accessToken;
}

export type BodyMeasurement = {
  id: string;
  weightKg: number;
  localDate: string;
  measuredAt: string;
};

export type WeightTrendPoint = {
  date: string;
  average: number;
  rollingAverage: number;
};

export type WeightTrend = {
  from: string | null;
  to: string | null;
  latest: number | null;
  latestOn: string | null;
  sevenDayChangeKg: number | null;
  points: WeightTrendPoint[];
};

/** Every logged reading, newest first (GET /me/body-measurements). */
export function useBodyMeasurements() {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["body-measurements"],
    queryFn: () => apiFetch<BodyMeasurement[]>("/api/v1/me/body-measurements", { accessToken }),
    enabled: accessToken != null,
  });
}

/** The daily-average + 7-day rolling-average trend (GET /analytics/weight). */
export function useWeightTrend() {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["weight-trend"],
    queryFn: () => apiFetch<WeightTrend>("/api/v1/analytics/weight", { accessToken }),
    enabled: accessToken != null,
  });
}

function useInvalidateWeight() {
  const qc = useQueryClient();
  return () => {
    qc.invalidateQueries({ queryKey: ["body-measurements"] });
    qc.invalidateQueries({ queryKey: ["weight-trend"] });
  };
}

export function useLogWeight() {
  const accessToken = useToken();
  const invalidate = useInvalidateWeight();
  return useMutation({
    mutationFn: (body: { weightKg: number; localDate?: string }) =>
      apiFetch<BodyMeasurement>("/api/v1/me/body-measurements", { method: "POST", body, accessToken }),
    onSuccess: invalidate,
  });
}

export function useDeleteWeight() {
  const accessToken = useToken();
  const invalidate = useInvalidateWeight();
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<void>(`/api/v1/me/body-measurements/${id}`, { method: "DELETE", accessToken }),
    onSuccess: invalidate,
  });
}

// --- nutrition adherence ---

export type NutrientTotals = { kcal: number; proteinG: number; carbG: number; fatG: number };

export type NutritionAnalytics = {
  from: string;
  to: string;
  targets: { kcal: number | null; proteinG: number | null; carbG: number | null; fatG: number | null };
  daysLogged: number;
  average: NutrientTotals;
  days: ({ date: string } & NutrientTotals)[];
};

export function useNutritionAnalytics() {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["nutrition-analytics"],
    queryFn: () => apiFetch<NutritionAnalytics>("/api/v1/analytics/nutrition", { accessToken }),
    enabled: accessToken != null,
  });
}
