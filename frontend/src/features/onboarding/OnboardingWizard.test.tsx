import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { OnboardingWizard } from "./OnboardingWizard";

const ESTIMATE = {
  formulaName: "mifflin-st-jeor",
  formulaVersion: "1.0",
  nutritionEstimateAvailable: true,
  unavailableReason: null,
  age: 31,
  bmr: 1481,
  activityFactor: 1.55,
  maintenanceCalories: 2295,
  goalAdjustment: -500,
  suggestedCalories: 1795,
  macros: { proteinGrams: 109, fatGrams: 54, carbGrams: 217, proteinFactor: 1.6, fatFactor: 0.8 },
  warnings: [],
  disclaimer: "This calorie target is an estimate. MySelf is a tracking tool, not medical advice.",
};

/** Routes fetch by URL: quiet the AuthProvider refresh, canned estimate + complete responses. */
function installFetch(overrides: { estimate?: unknown; complete?: unknown } = {}) {
  const spy = vi.fn<typeof fetch>((input) => {
    const url = typeof input === "string" ? input : input.toString();
    if (url.includes("/auth/refresh")) {
      return Promise.resolve(new Response(null, { status: 401 }));
    }
    if (url.includes("/me/nutrition-estimate")) {
      return Promise.resolve(
        new Response(JSON.stringify(overrides.estimate ?? ESTIMATE), {
          status: 200,
          headers: { "content-type": "application/json" },
        }),
      );
    }
    if (url.includes("/me/onboarding/complete")) {
      return Promise.resolve(
        new Response(JSON.stringify(overrides.complete ?? { id: "g1", goalType: "Lose", source: "Estimated" }), {
          status: 201,
          headers: { "content-type": "application/json" },
        }),
      );
    }
    return Promise.resolve(new Response(null, { status: 404 }));
  });
  vi.stubGlobal("fetch", spy);
  return spy;
}

function renderWizard() {
  const router = createMemoryRouter(
    [
      { path: "/onboarding", element: <OnboardingWizard /> },
      { path: "/dashboard", element: <div>Dashboard content</div> },
    ],
    { initialEntries: ["/onboarding"] },
  );
  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}

/** Complete step 1 with a valid adult profile and advance to step 2. */
async function fillAboutYou(user: ReturnType<typeof userEvent.setup>, { minor = false } = {}) {
  const dob = minor ? "2013-01-01" : "1994-03-21";
  fireEvent.change(screen.getByLabelText("Date of birth"), { target: { value: dob } });
  fireEvent.change(screen.getByLabelText("Height (cm)"), { target: { value: "178" } });
  fireEvent.change(screen.getByLabelText("Current weight (kg)"), { target: { value: "72" } });
  await user.click(screen.getByRole("radio", { name: "Male" }));
  await user.click(screen.getByRole("button", { name: "Continue" }));
}

describe("OnboardingWizard", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("blocks step 1 until the required fields are valid", async () => {
    installFetch();
    const user = userEvent.setup();
    renderWizard();

    await user.click(screen.getByRole("button", { name: "Continue" }));

    expect(await screen.findByText("Date of birth is required.")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "About you" })).toBeInTheDocument();
  });

  it("hides the sex control when the estimate is opted out", async () => {
    installFetch();
    const user = userEvent.setup();
    renderWizard();

    expect(screen.getByLabelText("Sex used for the estimate")).toBeInTheDocument();
    await user.click(screen.getByLabelText("Use my sex for the calorie estimate"));
    expect(screen.queryByLabelText("Sex used for the estimate")).not.toBeInTheDocument();
  });

  it("runs the estimate, shows the breakdown, and saves it", async () => {
    const fetchSpy = installFetch();
    const user = userEvent.setup();
    renderWizard();

    await fillAboutYou(user);
    await user.click(await screen.findByRole("radio", { name: /Lose weight/i }));
    await user.click(screen.getByRole("button", { name: "Continue" }));

    await user.click(await screen.findByRole("radio", { name: /Moderately active/i }));
    await user.click(screen.getByRole("radio", { name: /Standard/i }));
    await user.click(screen.getByRole("button", { name: "Review" }));

    expect(await screen.findByText("Suggested daily target")).toBeInTheDocument();
    // toLocaleString's grouping separator varies with the test env locale (1,795 / 1.795 / 1795).
    expect(screen.getByText(/1[.,]?795/)).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Use this estimate" }));

    expect(await screen.findByText("Dashboard content")).toBeInTheDocument();
    const completeCall = fetchSpy.mock.calls.find(([u]) => String(u).includes("/onboarding/complete"));
    expect(completeCall).toBeTruthy();
    expect(JSON.parse((completeCall![1] as RequestInit).body as string)).toMatchObject({
      goalType: "Lose",
      estimate: { weightKg: 72, activityLevel: "Moderate", pace: "Standard" },
    });
  });

  it("lets an adult skip nutrition setup entirely", async () => {
    const fetchSpy = installFetch();
    const user = userEvent.setup();
    renderWizard();

    await fillAboutYou(user);
    await user.click(await screen.findByRole("radio", { name: /Maintain weight/i }));
    await user.click(screen.getByRole("button", { name: "Continue" }));

    await user.click(await screen.findByRole("radio", { name: /Moderately active/i }));
    await user.click(screen.getByRole("button", { name: "Review" }));

    await user.click(await screen.findByRole("button", { name: "Skip nutrition setup" }));

    expect(await screen.findByText("Dashboard content")).toBeInTheDocument();
    const body = JSON.parse(
      (fetchSpy.mock.calls.find(([u]) => String(u).includes("/onboarding/complete"))![1] as RequestInit).body as string,
    );
    expect(body).toEqual({ goalType: "Maintain" });
  });

  it("offers only manual or skip for an under-18 user", async () => {
    installFetch();
    const user = userEvent.setup();
    renderWizard();

    await fillAboutYou(user, { minor: true });
    await user.click(await screen.findByRole("radio", { name: /Maintain weight/i }));
    await user.click(screen.getByRole("button", { name: "Review" }));

    expect(await screen.findByText(/don't calculate a calorie target for under-18s/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Use this estimate" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Set a target manually" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Skip nutrition setup" })).toBeInTheDocument();
  });
});
