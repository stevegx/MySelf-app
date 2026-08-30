import { NavLink, Outlet } from "react-router";
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
      className="inline-flex min-h-[34px] items-center gap-1.5 rounded-control border border-border-strong px-3 text-[13px] font-semibold hover:bg-surface-subtle"
    >
      <Icon size={15} aria-hidden />
      {MODE_LABEL[mode]}
    </button>
  );
}

export function AppShell() {
  return (
    <div className="flex min-h-screen flex-col items-start nav:flex-row">
      <aside className="flex w-full shrink-0 items-center gap-3 overflow-x-auto border-b border-border bg-surface p-4 nav:sticky nav:top-0 nav:h-screen nav:w-[232px] nav:flex-col nav:items-stretch nav:gap-6 nav:border-r nav:border-b-0 nav:px-4 nav:py-6">
        <div className="pl-2 text-xl font-bold tracking-tight">MySelf</div>

        <nav className="flex gap-0.5 nav:flex-col" aria-label="Primary">
          {NAV_ITEMS.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              className="flex min-h-11 items-center gap-2.5 rounded-control px-3 py-2.5 text-sm text-foreground no-underline hover:bg-surface-subtle aria-[current=page]:bg-primary-soft aria-[current=page]:font-semibold aria-[current=page]:text-primary-pressed"
            >
              <Icon size={18} aria-hidden />
              <span className="hidden nav:inline">{label}</span>
            </NavLink>
          ))}
        </nav>

        <div className="ml-auto flex items-center gap-3 nav:ml-0 nav:mt-auto nav:flex-col nav:items-stretch">
          <ThemeToggle />
          <div className="hidden items-center gap-2.5 rounded-control bg-surface-subtle px-2 py-2.5 nav:flex">
            <div className="flex size-[34px] shrink-0 items-center justify-center rounded-full bg-indigo-200 text-sm font-bold text-indigo-800">
              A
            </div>
            <div className="min-w-0">
              <div className="truncate text-[13px] font-bold">Alex Papadopoulos</div>
              <div className="text-[11px] text-foreground-muted">Free plan</div>
            </div>
          </div>
        </div>
      </aside>

      <main className="scrollbar-slim min-w-0 flex-1 p-5 nav:h-screen nav:overflow-y-auto nav:p-8 nav:pb-16">
        <div className="mx-auto max-w-[1120px]">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
