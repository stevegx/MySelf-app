import { z } from "zod";

/** A text input that must hold a number in [min, max]. Empty string fails with `message`. */
const numberInRange = (min: number, max: number, message: string) =>
  z.preprocess(
    (v) => (v === "" || v == null ? Number.NaN : Number(v)),
    z.number({ message }).min(min, message).max(max, message),
  );

/** Same, but an empty string is allowed and becomes `undefined`. */
const optionalNumberInRange = (min: number, max: number, message: string) =>
  z.preprocess(
    (v) => (v === "" || v == null ? undefined : Number(v)),
    z.number({ message }).min(min, message).max(max, message).optional(),
  );

export const GOAL_TYPES = ["Lose", "Maintain", "Gain", "TrackOnly"] as const;
export const ACTIVITY_LEVELS = ["Sedentary", "Light", "Moderate", "VeryActive", "ExtraActive"] as const;

export const aboutYouSchema = z
  .object({
    unitSystem: z.enum(["Metric", "Imperial"]),
    dateOfBirth: z
      .string()
      .min(1, "Date of birth is required.")
      .refine((s) => !Number.isNaN(Date.parse(s)), "Enter a valid date.")
      .refine((s) => new Date(s) < new Date(), "Date of birth must be in the past."),
    heightCm: numberInRange(50, 260, "Enter a height between 50 and 260 cm."),
    weightKg: numberInRange(20, 500, "Enter a weight between 20 and 500 kg."),
    useCalculationSex: z.boolean(),
    calculationSex: z.enum(["Male", "Female"]).optional(),
  })
  .superRefine((v, ctx) => {
    if (v.useCalculationSex && !v.calculationSex) {
      ctx.addIssue({ code: "custom", path: ["calculationSex"], message: "Choose Male or Female." });
    }
  });

export const goalSchema = z.object({
  goalType: z.enum(GOAL_TYPES),
  targetWeightKg: optionalNumberInRange(20, 500, "Enter a target weight between 20 and 500 kg."),
});

export const activitySchema = z
  .object({
    goalType: z.enum(GOAL_TYPES),
    activityLevel: z.enum(ACTIVITY_LEVELS),
    pace: z.enum(["Gentle", "Standard"]).optional(),
  })
  .superRefine((v, ctx) => {
    if ((v.goalType === "Lose" || v.goalType === "Gain") && !v.pace) {
      ctx.addIssue({ code: "custom", path: ["pace"], message: "Choose a pace." });
    }
  });

export const manualTargetSchema = z.object({
  calorieTarget: numberInRange(500, 20000, "Enter a calorie target between 500 and 20000."),
  proteinGrams: optionalNumberInRange(0, 2000, "0–2000 g."),
  carbGrams: optionalNumberInRange(0, 2000, "0–2000 g."),
  fatGrams: optionalNumberInRange(0, 2000, "0–2000 g."),
});

/** Flatten a ZodError into `{ field: firstMessage }` for inline display. */
export function fieldErrors(error: z.ZodError): Record<string, string> {
  const out: Record<string, string> = {};
  for (const issue of error.issues) {
    const key = String(issue.path[0] ?? "form");
    out[key] ??= issue.message;
  }
  return out;
}
