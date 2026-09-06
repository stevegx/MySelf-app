import {
  MUSCLE_GROUPS,
  activeGroupKeys,
  expandGroupsToMuscleNames,
  foldMusclesToGroups,
  groupLabels,
  muscleIdsForGroup,
  toggleGroup,
} from "./muscleGroups";
import type { MuscleGroup } from "./api";

// A stand-in for GET /api/v1/muscles — the 15 wger muscles, ids == wger ids.
const MUSCLES: MuscleGroup[] = [
  { id: 1, name: "Biceps", isFront: true },
  { id: 2, name: "Shoulders", isFront: true },
  { id: 3, name: "Serratus anterior", isFront: true },
  { id: 4, name: "Chest", isFront: true },
  { id: 5, name: "Triceps", isFront: false },
  { id: 6, name: "Abs", isFront: true },
  { id: 7, name: "Calves", isFront: false },
  { id: 8, name: "Glutes", isFront: false },
  { id: 9, name: "Trapezius", isFront: false },
  { id: 10, name: "Quads", isFront: true },
  { id: 11, name: "Hamstrings", isFront: false },
  { id: 12, name: "Lats", isFront: false },
  { id: 13, name: "Brachialis", isFront: true },
  { id: 14, name: "Obliquus externus abdominis", isFront: true },
  { id: 15, name: "Soleus", isFront: false },
];

describe("muscleGroups", () => {
  it("covers every catalogue muscle exactly once across the seven groups", () => {
    const grouped = MUSCLE_GROUPS.flatMap((g) => g.memberNames).sort();
    const all = MUSCLES.map((m) => m.name).sort();
    expect(grouped).toEqual(all);
    expect(MUSCLE_GROUPS).toHaveLength(7);
  });

  it("expands a group to the leaf muscle ids the catalogue returned", () => {
    expect(muscleIdsForGroup("legs", MUSCLES).sort((a, b) => a - b)).toEqual([7, 8, 10, 11, 15]);
    expect(muscleIdsForGroup("shoulders", MUSCLES).sort((a, b) => a - b)).toEqual([2, 3]);
    expect(muscleIdsForGroup("chest", MUSCLES)).toEqual([4]);
  });

  it("ignores member muscles the catalogue did not return", () => {
    const partial: MuscleGroup[] = [{ id: 10, name: "Quads", isFront: true }];
    expect(muscleIdsForGroup("legs", partial)).toEqual([10]);
  });

  it("toggles a whole group on, then off, back to the original set", () => {
    const on = toggleGroup("legs", [], MUSCLES);
    expect(on.sort((a, b) => a - b)).toEqual([7, 8, 10, 11, 15]);

    const off = toggleGroup("legs", on, MUSCLES);
    expect(off).toEqual([]);
  });

  it("treats a group as on when any one of its members is selected", () => {
    expect(activeGroupKeys([10], MUSCLES)).toEqual(["legs"]); // just Quads
    expect(activeGroupKeys([2, 4], MUSCLES).sort()).toEqual(["chest", "shoulders"]);
    expect(activeGroupKeys([], MUSCLES)).toEqual([]);
  });

  it("toggling off a partly-selected group clears all its members", () => {
    expect(toggleGroup("legs", [10, 8], MUSCLES)).toEqual([]);
  });

  it("leaves other groups' ids untouched when toggling one group", () => {
    const withChest = [4];
    const next = toggleGroup("biceps", withChest, MUSCLES);
    expect(next).toContain(4);
    expect(next).toEqual(expect.arrayContaining([1, 13]));
  });

  it("maps keys to human labels and to leaf names", () => {
    expect(groupLabels(["legs", "core"])).toEqual(["Legs", "Core"]);
    expect(expandGroupsToMuscleNames(["biceps"])).toEqual(["Biceps", "Brachialis"]);
  });

  it("folds per-muscle set counts into the seven groups, 0-filled and ordered", () => {
    const folded = foldMusclesToGroups([
      { label: "Quads", sets: 8 },
      { label: "Glutes", sets: 4 }, // also Legs
      { label: "Chest", sets: 6 },
      { label: "Brachialis", sets: 2 }, // Biceps
      { label: "Unknownus maximus", sets: 99 }, // ignored
    ]);

    expect(folded.map((g) => g.key)).toEqual(MUSCLE_GROUPS.map((g) => g.key));
    const byKey = Object.fromEntries(folded.map((g) => [g.key, g.sets]));
    expect(byKey.legs).toBe(12); // 8 + 4
    expect(byKey.chest).toBe(6);
    expect(byKey.biceps).toBe(2);
    expect(byKey.back).toBe(0);
    expect(byKey.core).toBe(0);
  });
});
