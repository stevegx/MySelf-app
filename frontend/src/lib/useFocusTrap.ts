import { useEffect, useRef } from "react";

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

// Body-scroll lock is reference-counted so stacked overlays don't fight over it.
let lockCount = 0;
function lockScroll() {
  if (lockCount === 0) {
    document.body.dataset.prevOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
  }
  lockCount += 1;
}
function unlockScroll() {
  lockCount = Math.max(0, lockCount - 1);
  if (lockCount === 0) {
    document.body.style.overflow = document.body.dataset.prevOverflow ?? "";
    delete document.body.dataset.prevOverflow;
  }
}

/**
 * Focus management for a modal/drawer overlay: on mount, remember what was focused and move
 * focus inside; lock background scroll; keep focus from escaping. On unmount, restore focus
 * to the trigger. Returns a ref for the container and an `onKeyDown` that traps Tab and
 * closes on Escape. The overlay is expected to be mounted only while open.
 */
export function useFocusTrap<T extends HTMLElement>(onClose: () => void, enabled = true) {
  const ref = useRef<T>(null);

  useEffect(() => {
    if (!enabled) return;

    const previouslyFocused = document.activeElement as HTMLElement | null;
    lockScroll();

    const container = ref.current;
    if (container && !container.contains(document.activeElement)) {
      (container.querySelector<HTMLElement>(FOCUSABLE) ?? container).focus();
    }

    const onFocusIn = (e: FocusEvent) => {
      if (container && e.target instanceof Node && !container.contains(e.target)) {
        e.stopPropagation();
        container.querySelector<HTMLElement>(FOCUSABLE)?.focus();
      }
    };
    document.addEventListener("focusin", onFocusIn);

    return () => {
      document.removeEventListener("focusin", onFocusIn);
      unlockScroll();
      if (previouslyFocused && document.contains(previouslyFocused)) {
        previouslyFocused.focus();
      }
    };
  }, [enabled]);

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (!enabled) return;
    if (e.key === "Escape") {
      e.stopPropagation();
      onClose();
      return;
    }
    if (e.key !== "Tab" || !ref.current) return;

    const items = Array.from(ref.current.querySelectorAll<HTMLElement>(FOCUSABLE)).filter(
      (el) => !el.hasAttribute("hidden") && el.getAttribute("aria-hidden") !== "true",
    );
    if (items.length === 0) {
      e.preventDefault();
      return;
    }
    const first = items[0];
    const last = items[items.length - 1];
    const active = document.activeElement;

    if (e.shiftKey && (active === first || !ref.current.contains(active))) {
      e.preventDefault();
      last.focus();
    } else if (!e.shiftKey && active === last) {
      e.preventDefault();
      first.focus();
    }
  };

  return { ref, onKeyDown };
}
