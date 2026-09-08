/**
 * The English message catalogue — the only launch language (locked decision #2).
 * A second language is a matter of adding a sibling file with the same shape and
 * selecting it in ./index.ts. Interpolation tokens are `{name}`.
 */
export const en = {
  common: {
    skipToContent: "Skip to main content",
    pageChanged: "Page changed",
    /** Announced after a client-side navigation. */
    pageAnnouncement: "{title} page",
  },
  nav: {
    primary: "Primary",
    dashboard: "Dashboard",
    workouts: "Workouts",
    nutrition: "Nutrition",
    progress: "Progress",
    settings: "Settings",
  },
  theme: {
    light: "Light",
    dark: "Dark",
    system: "System",
    /** aria-label for the cycling theme button. */
    toggle: "Theme: {current}. Switch to {next}.",
  },
} as const;

export type Messages = typeof en;
