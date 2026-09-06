import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { VolumeBalance } from "./VolumeBalance";
import type { RecentVolume } from "./api";

const base: RecentVolume = {
  from: "2026-08-23",
  to: "2026-09-05",
  sessions: 4,
  sets: 40,
  byDay: [
    { label: "Push", sets: 14 },
    { label: "Pull", sets: 12 },
    { label: "Legs", sets: 14 },
  ],
  byMuscle: [
    { label: "Chest", sets: 10 },
    { label: "Lats", sets: 8 },
    { label: "Quads", sets: 9 },
    { label: "Glutes", sets: 5 },
  ],
};

describe("VolumeBalance", () => {
  it("reads Balanced when every day carries volume", () => {
    render(<VolumeBalance recent={base} />);
    expect(screen.getByText("Balanced")).toBeInTheDocument();
    expect(screen.getByText(/4 sessions · 40 sets · last 14 days/)).toBeInTheDocument();
  });

  it("flags the neglected day when one sat at zero", () => {
    render(
      <VolumeBalance
        recent={{ ...base, byDay: [{ label: "Push", sets: 18 }, { label: "Pull", sets: 12 }, { label: "Legs", sets: 0 }] }}
      />,
    );
    expect(screen.getByText("Legs light")).toBeInTheDocument();
  });

  it("holds off judging until there's a baseline", () => {
    render(
      <VolumeBalance
        recent={{ ...base, sessions: 1, sets: 4, byDay: [{ label: "Push", sets: 4 }, { label: "Pull", sets: 0 }] }}
      />,
    );
    expect(screen.getByText("Building a baseline")).toBeInTheDocument();
  });

  it("switches to the seven muscle groups on the By muscle toggle", async () => {
    const user = userEvent.setup();
    render(<VolumeBalance recent={base} />);

    // Day view first.
    expect(screen.getByText("Push")).toBeInTheDocument();

    await user.click(screen.getByRole("radio", { name: "By muscle" }));
    for (const g of ["Chest", "Back", "Shoulders", "Biceps", "Triceps", "Legs", "Core"]) {
      expect(screen.getByText(g)).toBeInTheDocument();
    }
    // Back = Lats (8), and Shoulders/Triceps/Core had no logged sets -> flagged.
    expect(screen.getByText(/Shoulders (&|light)/)).toBeInTheDocument();
  });
});
