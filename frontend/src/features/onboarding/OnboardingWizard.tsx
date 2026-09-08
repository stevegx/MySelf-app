import { useState } from "react";
import { useNavigate } from "react-router";
import { Button, Checkbox, Field, Input, Segmented } from "../../components/ui";
import { cn } from "../../lib/cn";
import { ApiError } from "../../lib/api";
import {
  aboutYouSchema,
  activitySchema,
  fieldErrors,
  goalSchema,
  manualTargetSchema,
} from "./onboardingSchema";
import { DateOfBirthPicker } from "./DateOfBirthPicker";
import { useNutritionEstimate } from "./useNutritionEstimate";
import type { NutritionEstimate } from "./useNutritionEstimate";
import { useCompleteOnboarding } from "./useCompleteOnboarding";
import type { CompleteOnboardingInput } from "./useCompleteOnboarding";
import { useSaveProfile } from "./useSaveProfile";

type Answers = {
  unitSystem: "Metric" | "Imperial";
  dateOfBirth: string;
  heightCm: string;
  weightKg: string;
  useCalculationSex: boolean;
  calculationSex: "" | "Male" | "Female";
  goalType: "" | "Lose" | "Maintain" | "Gain" | "TrackOnly";
  targetWeightKg: string;
  activityLevel: "" | "Sedentary" | "Light" | "Moderate" | "VeryActive" | "ExtraActive";
  pace: "" | "Gentle" | "Standard";
};

const INITIAL: Answers = {
  unitSystem: "Metric",
  dateOfBirth: "",
  heightCm: "",
  weightKg: "",
  useCalculationSex: true,
  calculationSex: "",
  goalType: "",
  targetWeightKg: "",
  activityLevel: "",
  pace: "",
};

const GOAL_OPTIONS = [
  { value: "Lose", label: "Lose weight", hint: "Eat below maintenance" },
  { value: "Maintain", label: "Maintain weight", hint: "Stay where you are" },
  { value: "Gain", label: "Gain weight", hint: "Eat above maintenance" },
  { value: "TrackOnly", label: "Track only", hint: "No calorie target" },
] as const;

const ACTIVITY_OPTIONS = [
  { value: "Sedentary", label: "Sedentary", hint: "Little or no exercise, desk job" },
  { value: "Light", label: "Lightly active", hint: "1–3 workouts a week" },
  { value: "Moderate", label: "Moderately active", hint: "3–5 workouts a week" },
  { value: "VeryActive", label: "Very active", hint: "6–7 hard workouts a week" },
  { value: "ExtraActive", label: "Extra active", hint: "Hard daily training or a physical job" },
] as const;

function ageFrom(dob: string): number | null {
  if (!dob || Number.isNaN(Date.parse(dob))) return null;
  const d = new Date(dob);
  const now = new Date();
  let age = now.getFullYear() - d.getFullYear();
  const m = now.getMonth() - d.getMonth();
  if (m < 0 || (m === 0 && now.getDate() < d.getDate())) age -= 1;
  return age;
}

function formatSigned(n: number): string {
  const sign = n > 0 ? "+" : n < 0 ? "−" : "";
  return `${sign}${Math.abs(n).toLocaleString()}`;
}

function OptionList<T extends string>({
  name,
  value,
  onChange,
  options,
}: {
  name: string;
  value: T | "";
  onChange: (v: T) => void;
  options: readonly { value: T; label: string; hint?: string }[];
}) {
  return (
    <div role="radiogroup" aria-label={name} className="flex flex-col gap-2">
      {options.map((o) => (
        <label
          key={o.value}
          className={cn(
            "flex cursor-pointer flex-col gap-0.5 rounded-control border px-3.5 py-2.5 text-sm",
            "hover:bg-surface-subtle has-[:focus-visible]:outline has-[:focus-visible]:outline-2",
            "has-[:focus-visible]:-outline-offset-2 has-[:focus-visible]:outline-ring",
            value === o.value ? "border-primary bg-primary-soft" : "border-border",
          )}
        >
          <span className="flex items-center gap-2">
            <input
              type="radio"
              name={name}
              className="sr-only"
              checked={value === o.value}
              onChange={() => onChange(o.value)}
            />
            <span className="font-semibold">{o.label}</span>
          </span>
          {o.hint ? <span className="text-xs text-foreground-muted">{o.hint}</span> : null}
        </label>
      ))}
    </div>
  );
}

function ProgressBars({ filled }: { filled: number }) {
  return (
    <div className="flex gap-1.5">
      {[0, 1, 2, 3].map((i) => (
        <div
          key={i}
          className={cn("h-[5px] flex-1 rounded-full", i < filled ? "bg-primary" : "bg-border")}
        />
      ))}
    </div>
  );
}

function StepHeader({ step, title, subtitle }: { step: number; title: string; subtitle: string }) {
  return (
    <div>
      <div className="mb-1.5 text-[12px] uppercase tracking-[0.08em] text-foreground-muted">
        Step {step} of 4
      </div>
      <h1 className="mb-1 text-[26px]">{title}</h1>
      <p className="m-0 text-sm text-foreground-muted">{subtitle}</p>
    </div>
  );
}

function ErrorText({ children }: { children?: string }) {
  return children ? <span className="text-danger">{children}</span> : undefined;
}

const PACE_LABELS: Record<"Lose" | "Gain", { gentle: string; standard: string }> = {
  Lose: { gentle: "Gentle · −250 kcal/day", standard: "Standard · −500 kcal/day" },
  Gain: { gentle: "Gentle · +150 kcal/day", standard: "Standard · +300 kcal/day" },
};

export function OnboardingWizard() {
  const navigate = useNavigate();
  const estimateMutation = useNutritionEstimate();
  const completeMutation = useCompleteOnboarding();
  const saveProfileMutation = useSaveProfile();

  const [step, setStep] = useState(0);
  const [answers, setAnswers] = useState<Answers>(INITIAL);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [estimate, setEstimate] = useState<NutritionEstimate | null>(null);
  const [manualMode, setManualMode] = useState(false);
  const [manual, setManual] = useState({ calorieTarget: "", proteinGrams: "", carbGrams: "", fatGrams: "" });
  const [manualErrors, setManualErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  const set = <K extends keyof Answers>(key: K, value: Answers[K]) =>
    setAnswers((a) => ({ ...a, [key]: value }));

  const age = ageFrom(answers.dateOfBirth);
  const isMinor = age !== null && age < 18;
  const paceMatters = answers.goalType === "Lose" || answers.goalType === "Gain";
  const estimateEligible =
    !isMinor && answers.useCalculationSex && answers.goalType !== "TrackOnly" && answers.goalType !== "";

  const targetWeightForPayload =
    paceMatters && answers.targetWeightKg ? Number(answers.targetWeightKg) : undefined;

  const busy =
    estimateMutation.isPending || completeMutation.isPending || saveProfileMutation.isPending;

  async function goToReview() {
    setSubmitError(null);
    if (!estimateEligible) {
      setEstimate(null);
      setStep(3);
      return;
    }
    try {
      const result = await estimateMutation.mutateAsync({
        dateOfBirth: answers.dateOfBirth,
        calculationSex: answers.calculationSex as "Male" | "Female",
        heightCm: Number(answers.heightCm),
        weightKg: Number(answers.weightKg),
        activityLevel: answers.activityLevel,
        goalType: answers.goalType,
        pace: paceMatters ? answers.pace : null,
      });
      setEstimate(result);
      setStep(3);
    } catch (error) {
      setSubmitError(messageFor(error));
    }
  }

  function next() {
    setSubmitError(null);
    if (step === 0) {
      const parsed = aboutYouSchema.safeParse({
        unitSystem: answers.unitSystem,
        dateOfBirth: answers.dateOfBirth,
        heightCm: answers.heightCm,
        weightKg: answers.weightKg,
        useCalculationSex: answers.useCalculationSex,
        calculationSex: answers.useCalculationSex ? answers.calculationSex || undefined : undefined,
      });
      if (!parsed.success) return setErrors(fieldErrors(parsed.error));
      setErrors({});
      return setStep(1);
    }
    if (step === 1) {
      const parsed = goalSchema.safeParse({
        goalType: answers.goalType,
        targetWeightKg: answers.targetWeightKg,
        currentWeightKg: answers.weightKg,
      });
      if (!parsed.success) return setErrors(fieldErrors(parsed.error));
      setErrors({});
      return estimateEligible ? setStep(2) : goToReview();
    }
    if (step === 2) {
      const parsed = activitySchema.safeParse({
        goalType: answers.goalType,
        activityLevel: answers.activityLevel,
        pace: answers.pace || undefined,
      });
      if (!parsed.success) return setErrors(fieldErrors(parsed.error));
      setErrors({});
      return goToReview();
    }
  }

  function back() {
    setErrors({});
    setSubmitError(null);
    setManualMode(false);
    if (step === 3) return setStep(estimateEligible ? 2 : 1);
    setStep((s) => Math.max(0, s - 1));
  }

  async function finish(input: CompleteOnboardingInput) {
    setSubmitError(null);
    try {
      // The profile (DOB / height / sex) must be persisted first — POST /me/onboarding/complete
      // reads it and 400s if it's missing. PUT /me/profile is create-or-update / idempotent.
      await saveProfileMutation.mutateAsync({
        unitSystem: answers.unitSystem,
        dateOfBirth: answers.dateOfBirth,
        heightCm: Number(answers.heightCm),
        calculationSex: answers.useCalculationSex ? (answers.calculationSex || null) : null,
        timezone: Intl.DateTimeFormat().resolvedOptions().timeZone || null,
      });
      await completeMutation.mutateAsync(input);
      navigate("/dashboard", { replace: true });
    } catch (error) {
      // 409 = onboarding already done (e.g. a stale tab). The user's intent is satisfied.
      if (error instanceof ApiError && error.status === 409) {
        return navigate("/dashboard", { replace: true });
      }
      setSubmitError(messageFor(error));
    }
  }

  const useEstimate = () =>
    finish({
      goalType: answers.goalType,
      targetWeightKg: targetWeightForPayload,
      estimate: {
        weightKg: Number(answers.weightKg),
        activityLevel: answers.activityLevel,
        pace: paceMatters ? answers.pace : null,
      },
    });

  const skipNutrition = () =>
    finish({ goalType: answers.goalType, targetWeightKg: targetWeightForPayload });

  function openManual() {
    setManual({
      calorieTarget: estimate?.suggestedCalories?.toString() ?? "",
      proteinGrams: estimate?.macros?.proteinGrams?.toString() ?? "",
      carbGrams: estimate?.macros?.carbGrams?.toString() ?? "",
      fatGrams: estimate?.macros?.fatGrams?.toString() ?? "",
    });
    setManualErrors({});
    setManualMode(true);
  }

  function saveManual() {
    const parsed = manualTargetSchema.safeParse(manual);
    if (!parsed.success) return setManualErrors(fieldErrors(parsed.error));
    finish({
      goalType: answers.goalType,
      targetWeightKg: targetWeightForPayload,
      manualTarget: {
        calorieTarget: parsed.data.calorieTarget,
        proteinGrams: parsed.data.proteinGrams,
        carbGrams: parsed.data.carbGrams,
        fatGrams: parsed.data.fatGrams,
      },
    });
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-5 py-12">
      <div className="flex w-[min(520px,100%)] flex-col gap-5 rounded-card border border-border bg-surface p-9 pb-7 shadow-md">
        <ProgressBars filled={step + 1} />

        {step === 0 && (
          <>
            <StepHeader step={1} title="About you" subtitle="Used only to personalise your estimate." />
            <Field label="Units">
              <Segmented<"Metric" | "Imperial">
                aria-label="Unit system"
                value={answers.unitSystem}
                onChange={(v) => set("unitSystem", v)}
                options={[
                  { value: "Metric", label: "Metric (kg, cm)" },
                  { value: "Imperial", label: "Imperial" },
                ]}
              />
            </Field>
            <Field label="Date of birth" hint={<ErrorText>{errors.dateOfBirth}</ErrorText>}>
              <DateOfBirthPicker
                value={answers.dateOfBirth}
                onChange={(v) => set("dateOfBirth", v)}
                invalid={errors.dateOfBirth ? true : undefined}
              />
            </Field>
            {isMinor && (
              <p className="m-0 rounded-control bg-info-soft px-3 py-2 text-xs text-info">
                We don&apos;t calculate calorie targets for under-18s. You can still set one manually or
                skip nutrition for now.
              </p>
            )}
            <Field label="Height (cm)" htmlFor="height" hint={<ErrorText>{errors.heightCm}</ErrorText>}>
              <Input
                id="height"
                type="number"
                inputMode="decimal"
                value={answers.heightCm}
                onChange={(e) => set("heightCm", e.target.value)}
                aria-invalid={errors.heightCm ? true : undefined}
              />
            </Field>
            <Field label="Current weight (kg)" htmlFor="weight" hint={<ErrorText>{errors.weightKg}</ErrorText>}>
              <Input
                id="weight"
                type="number"
                inputMode="decimal"
                value={answers.weightKg}
                onChange={(e) => set("weightKg", e.target.value)}
                aria-invalid={errors.weightKg ? true : undefined}
              />
            </Field>
            <Checkbox
              id="useCalcSex"
              label="Use my sex for the calorie estimate"
              checked={answers.useCalculationSex}
              onChange={(e) => set("useCalculationSex", e.target.checked)}
            />
            {answers.useCalculationSex && (
              <Field
                label="Sex used for the estimate"
                hint={
                  errors.calculationSex ? (
                    <ErrorText>{errors.calculationSex}</ErrorText>
                  ) : (
                    "Only the Mifflin–St Jeor formula uses this."
                  )
                }
              >
                <Segmented<"Male" | "Female">
                  aria-label="Sex used for the estimate"
                  value={answers.calculationSex || ("" as "Male")}
                  onChange={(v) => set("calculationSex", v)}
                  options={[
                    { value: "Male", label: "Male" },
                    { value: "Female", label: "Female" },
                  ]}
                />
              </Field>
            )}
            <div className="mt-1 flex justify-end">
              <Button variant="primary" onClick={next}>
                Continue
              </Button>
            </div>
          </>
        )}

        {step === 1 && (
          <>
            <StepHeader step={2} title="Your goal" subtitle="What are you working towards right now?" />
            <OptionList
              name="Goal"
              value={answers.goalType}
              onChange={(v) => set("goalType", v)}
              options={GOAL_OPTIONS}
            />
            {errors.goalType && <p className="m-0 text-xs text-danger">{errors.goalType}</p>}
            {paceMatters && (
              <Field
                label="Target weight (kg) — optional"
                htmlFor="targetWeight"
                hint={
                  errors.targetWeightKg ? (
                    <ErrorText>{errors.targetWeightKg}</ErrorText>
                  ) : (
                    "For context only. MySelf never promises a date."
                  )
                }
              >
                <Input
                  id="targetWeight"
                  type="number"
                  inputMode="decimal"
                  value={answers.targetWeightKg}
                  onChange={(e) => set("targetWeightKg", e.target.value)}
                  aria-invalid={errors.targetWeightKg ? true : undefined}
                />
              </Field>
            )}
            <div className="mt-1 flex justify-between">
              <Button variant="ghost" onClick={back}>
                ← Back
              </Button>
              <Button variant="primary" onClick={next} disabled={busy}>
                {estimateEligible ? "Continue" : "Review"}
              </Button>
            </div>
          </>
        )}

        {step === 2 && (
          <>
            <StepHeader
              step={3}
              title="Activity and pace"
              subtitle="Pick a typical week — not your most active one."
            />
            <OptionList
              name="Activity level"
              value={answers.activityLevel}
              onChange={(v) => set("activityLevel", v)}
              options={ACTIVITY_OPTIONS}
            />
            {errors.activityLevel && <p className="m-0 text-xs text-danger">{errors.activityLevel}</p>}
            {paceMatters && (
              <Field label="Pace" hint={<ErrorText>{errors.pace}</ErrorText>}>
                <Segmented<"Gentle" | "Standard">
                  aria-label="Pace"
                  value={answers.pace || ("" as "Gentle")}
                  onChange={(v) => set("pace", v)}
                  options={[
                    { value: "Gentle", label: PACE_LABELS[answers.goalType as "Lose" | "Gain"].gentle },
                    { value: "Standard", label: PACE_LABELS[answers.goalType as "Lose" | "Gain"].standard },
                  ]}
                />
              </Field>
            )}
            <div className="mt-1 flex justify-between">
              <Button variant="ghost" onClick={back}>
                ← Back
              </Button>
              <Button variant="primary" onClick={next} disabled={busy}>
                {busy ? "Calculating…" : "Review"}
              </Button>
            </div>
          </>
        )}

        {step === 3 && (
          <>
            <StepHeader
              step={4}
              title="Review your estimate"
              subtitle="Based on what you told us. You can always adjust this later."
            />

            {estimate?.nutritionEstimateAvailable ? (
              <div className="flex flex-col overflow-hidden rounded-control border border-border">
                <Row label="Estimated BMR" value={`${estimate.bmr?.toLocaleString()} kcal/day`} />
                <Row label="Activity estimate" value={`× ${estimate.activityFactor}`} />
                <Row
                  label="Estimated maintenance"
                  value={`${estimate.maintenanceCalories?.toLocaleString()} kcal/day`}
                />
                <Row label="Goal adjustment" value={`${formatSigned(estimate.goalAdjustment ?? 0)} kcal/day`} />
                <div className="flex justify-between bg-primary-soft px-4 py-3.5">
                  <span className="text-sm font-bold">Suggested daily target</span>
                  <span className="text-lg font-bold text-primary-pressed">
                    {estimate.suggestedCalories?.toLocaleString()} kcal
                  </span>
                </div>
              </div>
            ) : (
              <p className="m-0 rounded-control bg-surface-subtle px-4 py-3 text-sm text-foreground-muted">
                {reviewUnavailableMessage(answers, estimate)}
              </p>
            )}

            {estimate?.warnings?.map((w) => (
              <p
                key={w.code}
                className="m-0 rounded-control border border-warning/40 bg-warning-soft px-3 py-2 text-xs text-warning"
              >
                {w.message}
              </p>
            ))}

            <p className="m-0 text-xs leading-relaxed text-foreground-muted">
              {estimate?.disclaimer ??
                "This calorie target is an estimate. MySelf is a tracking tool, not medical advice, and does not replace a doctor, registered dietitian, or qualified trainer."}
            </p>

            {submitError && (
              <div
                role="alert"
                className="rounded-control border border-danger/40 bg-danger-soft px-3 py-2 text-sm text-danger"
              >
                {submitError}
              </div>
            )}

            {manualMode ? (
              <div className="flex flex-col gap-3 rounded-control border border-border p-3">
                <Field
                  label="Calorie target"
                  htmlFor="mt-cal"
                  hint={<ErrorText>{manualErrors.calorieTarget}</ErrorText>}
                >
                  <Input
                    id="mt-cal"
                    type="number"
                    inputMode="numeric"
                    value={manual.calorieTarget}
                    onChange={(e) => setManual((m) => ({ ...m, calorieTarget: e.target.value }))}
                    aria-invalid={manualErrors.calorieTarget ? true : undefined}
                  />
                </Field>
                <div className="grid grid-cols-3 gap-2">
                  <Field label="Protein (g)" htmlFor="mt-p" hint={<ErrorText>{manualErrors.proteinGrams}</ErrorText>}>
                    <Input
                      id="mt-p"
                      type="number"
                      value={manual.proteinGrams}
                      onChange={(e) => setManual((m) => ({ ...m, proteinGrams: e.target.value }))}
                    />
                  </Field>
                  <Field label="Carbs (g)" htmlFor="mt-c" hint={<ErrorText>{manualErrors.carbGrams}</ErrorText>}>
                    <Input
                      id="mt-c"
                      type="number"
                      value={manual.carbGrams}
                      onChange={(e) => setManual((m) => ({ ...m, carbGrams: e.target.value }))}
                    />
                  </Field>
                  <Field label="Fat (g)" htmlFor="mt-f" hint={<ErrorText>{manualErrors.fatGrams}</ErrorText>}>
                    <Input
                      id="mt-f"
                      type="number"
                      value={manual.fatGrams}
                      onChange={(e) => setManual((m) => ({ ...m, fatGrams: e.target.value }))}
                    />
                  </Field>
                </div>
                <div className="flex gap-2">
                  <Button variant="primary" className="flex-1" onClick={saveManual} disabled={busy}>
                    {busy ? "Saving…" : "Save target"}
                  </Button>
                  <Button variant="secondary" onClick={() => setManualMode(false)} disabled={busy}>
                    Cancel
                  </Button>
                </div>
              </div>
            ) : (
              <div className="flex flex-col gap-2">
                {estimate?.nutritionEstimateAvailable && (
                  <Button variant="primary" block onClick={useEstimate} disabled={busy}>
                    {busy ? "Saving…" : "Use this estimate"}
                  </Button>
                )}
                <div className="flex gap-2">
                  <Button variant="secondary" className="flex-1" onClick={openManual} disabled={busy}>
                    Set a target manually
                  </Button>
                  <Button variant="secondary" className="flex-1" onClick={skipNutrition} disabled={busy}>
                    Skip nutrition setup
                  </Button>
                </div>
                <div className="mt-1 flex justify-start">
                  <Button variant="ghost" onClick={back} disabled={busy}>
                    ← Back
                  </Button>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between border-b border-border px-4 py-3">
      <span className="text-sm text-foreground-muted">{label}</span>
      <span className="text-sm">{value}</span>
    </div>
  );
}

function reviewUnavailableMessage(answers: Answers, estimate: NutritionEstimate | null): string {
  const reason = estimate?.unavailableReason;
  if (reason === "under-18" || (ageFrom(answers.dateOfBirth) ?? 99) < 18) {
    return "We don't calculate a calorie target for under-18s. Set one manually or skip for now.";
  }
  if (reason === "track-only" || answers.goalType === "TrackOnly") {
    return "A track-only goal doesn't use a calorie target. You can still set one manually.";
  }
  return "You chose not to use the calorie estimate. Set a target manually or skip for now.";
}

function messageFor(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.errors) {
      const first = Object.values(error.errors)[0]?.[0];
      if (first) return first;
    }
    return error.detail ?? error.title;
  }
  return "Something went wrong. Please try again.";
}
