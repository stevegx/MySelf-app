import { createContext, useContext } from "react";

export type ThemeMode = "light" | "dark" | "system";
export type ResolvedTheme = "light" | "dark";

export type ThemeContextValue = {
  /** What the user picked. */
  mode: ThemeMode;
  /** What is actually applied right now (system resolved to light/dark). */
  resolved: ResolvedTheme;
  setMode: (mode: ThemeMode) => void;
};

export const THEME_STORAGE_KEY = "myself.theme";

export const ThemeContext = createContext<ThemeContextValue | null>(null);

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) {
    throw new Error("useTheme must be used within <ThemeProvider>");
  }
  return ctx;
}
