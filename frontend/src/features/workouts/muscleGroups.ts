import type { MuscleGroup } from "./api";

/**
 * The catalogue ships ~15 fine-grained wger muscles (Brachialis, Serratus anterior,
 * Obliquus externus abdominis, Soleus, …). That detail is right for *stats* — muscle
 * coverage, weekly sets — but far too much for *picking* what a day trains. For input
 * (day focus, exercise-picker filtering) we collapse them to seven plain groups.
 *
 * This is a UI-only concern, so it lives here rather than in the domain: `FocusMuscleIds`
 * still stores the leaf muscle ids, and the stats endpoints still report per-muscle.
 * Selecting the "Legs" group just writes all of its member muscle ids at once.
 */
export const MUSCLE_GROUPS = [
  { key: "chest", label: "Chest", memberNames: ["Chest"] },
  { key: "back", label: "Back", memberNames: ["Lats", "Trapezius"] },
  { key: "shoulders", label: "Shoulders", memberNames: ["Shoulders", "Serratus anterior"] },
  { key: "biceps", label: "Biceps", memberNames: ["Biceps", "Brachialis"] },
  { key: "triceps", label: "Triceps", memberNames: ["Triceps"] },
  { key: "legs", label: "Legs", memberNames: ["Quads", "Hamstrings", "Glutes", "Calves", "Soleus"] },
  { key: "core", label: "Core", memberNames: ["Abs", "Obliquus externus abdominis"] },
] as const;

export type MuscleGroupKey = (typeof MUSCLE_GROUPS)[number]["key"];

const groupByKey = new Map(MUSCLE_GROUPS.map((g) => [g.key, g]));

/** Leaf muscle ids for a group, restricted to muscles the catalogue actually returned. */
export function muscleIdsForGroup(key: MuscleGroupKey, muscles: MuscleGroup[]): number[] {
  const names = new Set<string>(groupByKey.get(key)?.memberNames ?? []);
  return muscles.filter((m) => names.has(m.name)).map((m) => m.id);
}

/** Every leaf muscle name across the given groups — what the picker filters on. */
export function expandGroupsToMuscleNames(keys: readonly MuscleGroupKey[]): string[] {
  return keys.flatMap((k) => [...(groupByKey.get(k)?.memberNames ?? [])]);
}

/** Which groups a focus selection touches — "on" if any of its member muscles is present. */
export function activeGroupKeys(focusMuscleIds: number[], muscles: MuscleGroup[]): MuscleGroupKey[] {
  const focus = new Set(focusMuscleIds);
  return MUSCLE_GROUPS.filter((g) => muscleIdsForGroup(g.key, muscles).some((id) => focus.has(id))).map(
    (g) => g.key,
  );
}

/**
 * Toggle a group in a focus id list: add all of its member ids if the group is currently
 * off, remove all of them if it's on. Symmetric, so a toggle round-trips to the same set.
 */
export function toggleGroup(
  key: MuscleGroupKey,
  focusMuscleIds: number[],
  muscles: MuscleGroup[],
): number[] {
  const ids = muscleIdsForGroup(key, muscles);
  const on = ids.some((id) => focusMuscleIds.includes(id));
  if (on) {
    const drop = new Set(ids);
    return focusMuscleIds.filter((id) => !drop.has(id));
  }
  const have = new Set(focusMuscleIds);
  return [...focusMuscleIds, ...ids.filter((id) => !have.has(id))];
}

export const groupLabels = (keys: readonly MuscleGroupKey[]): string[] =>
  keys.map((k) => groupByKey.get(k)?.label ?? k);
