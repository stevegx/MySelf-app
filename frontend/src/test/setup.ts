import "@testing-library/jest-dom/vitest";

// Every test starts network-safe: AuthProvider (features/auth/AuthContext.tsx) fires a
// silent POST /auth/refresh on mount to check for a "trust this device" session, in every
// test that renders <Providers>. Without a default mock, that call would either hit real
// jsdom/Node fetch (network error / hang) or clash with a test's own fetch stub set up
// before render. A plain 401 here means "not logged in", the normal baseline outcome.
// Tests that need their own fetch behavior call vi.stubGlobal("fetch", ...) themselves,
// which simply replaces this default for the rest of that test.
beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 401 })));
});

afterEach(() => {
  vi.unstubAllGlobals();
});
