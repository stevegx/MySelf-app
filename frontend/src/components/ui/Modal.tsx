import { useId } from "react";
import type { ReactNode } from "react";
import { createPortal } from "react-dom";
import { cn } from "../../lib/cn";
import { useFocusTrap } from "../../lib/useFocusTrap";

type ModalSize = "sm" | "md" | "lg";

type ModalProps = {
  /** Visible heading, rendered as an <h2>. Also the accessible name unless `label` is set. */
  title: ReactNode;
  /** Overrides the accessible name (aria-label) when it needs to differ from the visible title. */
  label?: string;
  onClose: () => void;
  children: ReactNode;
  size?: ModalSize;
  /** Clicking the backdrop closes the modal (default true). */
  closeOnBackdrop?: boolean;
  /** Extra classes for the panel. */
  className?: string;
};

const SIZE: Record<ModalSize, string> = {
  sm: "max-w-sm",
  md: "max-w-md",
  lg: "max-w-lg",
};

/**
 * An accessible modal dialog: rendered in a portal, focus is moved in on open and restored
 * to the trigger on close, Tab is trapped inside, Escape closes, and background scroll is
 * locked (see {@link useFocusTrap}). Replaces the ad-hoc `<div role="dialog">` blocks —
 * those set `aria-modal` without actually trapping focus.
 */
export function Modal({
  title,
  label,
  onClose,
  children,
  size = "sm",
  closeOnBackdrop = true,
  className,
}: ModalProps) {
  const { ref, onKeyDown } = useFocusTrap<HTMLDivElement>(onClose);
  const titleId = useId();

  return createPortal(
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      onMouseDown={(e) => {
        if (closeOnBackdrop && e.target === e.currentTarget) onClose();
      }}
      onKeyDown={onKeyDown}
    >
      <div
        ref={ref}
        role="dialog"
        aria-modal="true"
        aria-labelledby={label ? undefined : titleId}
        aria-label={label}
        tabIndex={-1}
        className={cn(
          "flex w-full flex-col gap-3 rounded-card border border-border bg-surface p-4 shadow-xl outline-none",
          SIZE[size],
          className,
        )}
      >
        <h2 id={titleId} className="m-0 text-base font-bold">
          {title}
        </h2>
        {children}
      </div>
    </div>,
    document.body,
  );
}
