import { useEffect, useRef, useState } from "react";
import { NavLink, Outlet, useLocation } from "react-router";
import {
  Dumbbell,
  LayoutDashboard,
  Monitor,
  Moon,
  Settings,
  Sun,
  TrendingUp,
  Utensils,
} from "lucide-react";
import { useAuth } from "../features/auth/auth";
import { useMe } from "../features/auth/useMe";
import { ResumeWorkoutBar } from "../features/workouts/ResumeWorkoutBar";
import { useTheme } from "./theme";
import type { ThemeMode } from "./theme";

const NAV_ITEMS = [
  { to: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  { to: "/workouts", label: "Workouts", icon: Dumbbell },
  { to: "/nutrition", label: "Nutrition", icon: Utensils },
  { to: "/progress", label: "Progress", icon: TrendingUp },
  { to: "/settings", label: "Settings", icon: Settings },
];

const NEXT_MODE: Record<ThemeMode, ThemeMode> = {
  light: "dark",
  dark: "system",
  system: "light",
};

const MODE_ICON = { light: Sun, dark: Moon, system: Monitor };
const MODE_LABEL = { light: "Light", dark: "Dark", system: "System" };

function ThemeToggle() {
  const { mode, setMode } = useTheme();
  const Icon = MODE_ICON[mode];
  return (
    <button
      type="button"
      onClick={() => setMode(NEXT_MODE[mode])}
      aria-label={`Theme: ${MODE_LABEL[mode]}. Switch to ${MODE_LABEL[NEXT_MODE[mode]]}.`}
      className="inline-flex min-h-[34px] items-center gap-1.5 rounded-control border border-border-strong px-3 text-sm font-semibold hover:bg-surface-subtle"
    >
      <Icon size={15} aria-hidden />
      {MODE_LABEL[mode]}
    </button>
  );
}

function AccountSummary() {
  const { session } = useAuth();
  // GET /api/v1/me is the protected endpoint behind the JWT-bearer middleware — this is
  // what actually proves it works, rather than only ever trusting the cached login/register
  // response. Falls back to that cached user while the request is in flight.
  const { data: me } = useMe();
  const user = me?.user ?? session?.user;

  if (!user) {
    return null;
  }

  return (
    <div className="hidden items-center gap-2.5 rounded-control bg-surface-subtle px-2 py-2.5 nav:flex">
      <div className="flex size-[34px] shrink-0 items-center justify-center rounded-full bg-indigo-200 text-sm font-bold text-indigo-800">
        {user.username.charAt(0).toUpperCase()}
      </div>
      <div className="min-w-0">
        <div className="truncate text-sm font-bold">{user.username}</div>
        <div className="truncate text-[12px] text-foreground-muted">{user.email}</div>
      </div>
    </div>
  );
}

const navLinkClass =
  "flex min-h-11 items-center gap-2.5 rounded-control px-3 py-2.5 text-sm text-foreground no-underline hover:bg-surface-subtle aria-[current=page]:bg-primary-soft aria-[current=page]:font-semibold aria-[current=page]:text-primary-pressed";

/**
 * Announces the new page to screen readers after a client-side navigation (which otherwise
 * moves nothing and says nothing). Reads the page's <h1> once the route has rendered.
 */
function RouteAnnouncer() {
  const { pathname } = useLocation();
  const [message, setMessage] = useState("");
  const first = useRef(true);

  useEffect(() => {
    if (first.current) {
      first.current = false;
      return;
    }
    const id = requestAnimationFrame(() => {
      const heading = document.querySelector<HTMLElement>("#main-content h1")?.textContent?.trim();
      setMessage(heading ? `${heading} page` : "Page changed");
    });
    return () => cancelAnimationFrame(id);
  }, [pathname]);

  return (
    <div aria-live="polite" role="status" className="sr-only">
      {message}
    </div>
  );
}

export function AppShell() {
  return (
    <div className="flex min-h-screen flex-col items-start nav:flex-row">
      <RouteAnnouncer />
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded-control focus:bg-surface focus:px-4 focus:py-2 focus:text-sm focus:font-semibold focus:shadow-md focus:outline focus:outline-2 focus:outline-ring"
      >
        Skip to main content
      </a>
      <aside className="flex w-full shrink-0 items-center gap-3 border-b border-border bg-surface p-4 nav:sticky nav:top-0 nav:h-screen nav:w-[232px] nav:flex-col nav:items-stretch nav:gap-6 nav:border-r nav:border-b-0 nav:px-4 nav:py-6">
        <div className="pl-2 text-xl font-bold tracking-tight">MySelf</div>

        {/* Desktop: the nav lives in the sidebar. Mobile: it moves to a bottom bar (below). */}
        <nav className="hidden nav:flex nav:flex-col nav:gap-0.5" aria-label="Primary">
          {NAV_ITEMS.map(({ to, label, icon: Icon }) => (
            <NavLink key={to} to={to} className={navLinkClass}>
              <Icon size={18} aria-hidden />
              <span>{label}</span>
            </NavLink>
          ))}
        </nav>

        <div className="ml-auto flex items-center gap-3 nav:ml-0 nav:mt-auto nav:flex-col nav:items-stretch">
          <ThemeToggle />
          <AccountSummary />
        </div>
      </aside>

      <main
        id="main-content"
        tabIndex={-1}
        className="scrollbar-slim min-w-0 flex-1 p-5 pb-[calc(4.5rem+env(safe-area-inset-bottom))] outline-none nav:h-screen nav:overflow-y-auto nav:p-8 nav:pb-16"
      >
        <div className="mx-auto max-w-[1120px]">
          <Outlet />
        </div>
      </main>

      <ResumeWorkoutBar />

      {/* Mobile bottom tab bar. */}
      <nav
        aria-label="Primary"
        className="fixed inset-x-0 bottom-0 z-30 flex border-t border-border bg-surface pb-[env(safe-area-inset-bottom)] nav:hidden"
      >
        {NAV_ITEMS.map(({ to, label, icon: Icon }) => (
          <NavLink
            key={to}
            to={to}
            className="flex flex-1 flex-col items-center gap-0.5 py-2 text-[11px] text-foreground-muted no-underline aria-[current=page]:text-primary-pressed"
          >
            <Icon size={20} aria-hidden />
            {label}
          </NavLink>
        ))}
      </nav>
    </div>
  );
}
