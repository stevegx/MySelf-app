import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { WorkoutCalendarScreen } from "./WorkoutCalendarScreen";

const AUTH = { accessToken: "t", user: { id: "u1", username: "d", email: "d@e.com" } };

/** A session on the 15th of the current month, in the visible grid. */
function thisMonthDate(day: number) {
  const n = new Date();
  return `${n.getFullYear()}-${String(n.getMonth() + 1).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

function installFetch() {
  const date = thisMonthDate(15);
  const spy = vi.fn<typeof fetch>((input) => {
    const url = String(input);
    const json = (b: unknown) =>
      Promise.resolve(new Response(JSON.stringify(b), { status: 200, headers: { "content-type": "application/json" } }));
    if (url.includes("/auth/refresh")) return json(AUTH);
    if (url.includes("/api/v1/workout-calendar")) {
      return json({
        from: date,
        to: date,
        days: [
          {
            date,
            sessions: [
              {
                id: "s1",
                dayName: "Legs A",
                programName: "PPL",
                status: "Completed",
                startedAt: `${date}T09:00:00Z`,
                completedAt: `${date}T10:00:00Z`,
                performedOnLocalDate: date,
                summary: { durationSeconds: 3600, completedSetCount: 5, skippedSetCount: 0, totalReps: 40, totalVolumeKg: 3200 },
              },
            ],
          },
        ],
      });
    }
    return Promise.resolve(new Response(null, { status: 404 }));
  });
  vi.stubGlobal("fetch", spy);
  return spy;
}

function renderScreen() {
  const router = createMemoryRouter(
    [
      { path: "/", element: <WorkoutCalendarScreen /> },
      { path: "/workouts/history", element: <div>List</div> },
    ],
    { initialEntries: ["/"] },
  );
  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}

describe("WorkoutCalendarScreen", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("marks a day with a session and opens it on click", async () => {
    installFetch();
    const user = userEvent.setup();
    renderScreen();

    // The 15th has a session marker.
    const cell = await screen.findByRole("button", { name: /1 session/i });
    await user.click(cell);

    await waitFor(() => expect(screen.getByText("Legs A")).toBeInTheDocument());
    expect(screen.getByText("5 sets")).toBeInTheDocument();
  });
});
