import { en } from "./en";
import type { Messages } from "./en";

/**
 * The active catalogue. English-only at launch (decision #2); this is the single seam a
 * second language plugs into — add `el.ts` with the same shape and select it here from
 * `navigator.language` or the user's saved `UserProfile.Locale`. Kept dependency-free on
 * purpose; `react-i18next` is the upgrade path once real translations exist.
 */
const catalogue: Messages = en;

/**
 * Resolve one message. `select` picks it from the catalogue (fully type-checked, no
 * stringly-typed keys); `vars` fills `{token}` placeholders.
 *
 *   t((m) => m.nav.dashboard)
 *   t((m) => m.theme.toggle, { current: "Light", next: "Dark" })
 */
export function t(
  select: (m: Messages) => string,
  vars?: Record<string, string | number>,
): string {
  let text = select(catalogue);
  if (vars) {
    for (const [key, value] of Object.entries(vars)) {
      text = text.split(`{${key}}`).join(String(value));
    }
  }
  return text;
}

/** Hook form, for symmetry with a future context-based implementation. */
export function useT() {
  return t;
}

export type { Messages };
