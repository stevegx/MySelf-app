import { cloneElement, isValidElement, useId } from "react";
import type { ReactElement, ReactNode } from "react";
import { cn } from "../../lib/cn";

type FieldProps = {
  label: ReactNode;
  htmlFor?: string;
  /** Neutral helper text under the control. */
  hint?: ReactNode;
  /** Error message under the control. Takes over from `hint`, is announced, and marks the control invalid. */
  error?: ReactNode;
  className?: string;
  children: ReactNode;
};

/**
 * A labelled form control. When the single child is an element, the hint/error is wired to
 * it with `aria-describedby` (and `aria-invalid` when there's an error) so a screen reader
 * announces it on focus. The error is also given `role="alert"`.
 */
export function Field({ label, htmlFor, hint, error, className, children }: FieldProps) {
  const fallbackId = useId();
  const messageId = htmlFor ? `${htmlFor}-desc` : fallbackId;
  const message = error ?? hint;

  let control = children;
  if (isValidElement(children) && message != null) {
    const el = children as ReactElement<Record<string, unknown>>;
    const existing = el.props["aria-describedby"];
    control = cloneElement(el, {
      "aria-describedby": [existing, messageId].filter(Boolean).join(" "),
      "aria-invalid": error != null ? true : el.props["aria-invalid"],
    });
  }

  return (
    <div className={cn("flex flex-col", className)}>
      <label htmlFor={htmlFor} className="mb-1.5 text-xs text-foreground-muted">
        {label}
      </label>
      {control}
      {error != null ? (
        <span id={messageId} role="alert" className="mt-1 text-xs text-danger">
          {error}
        </span>
      ) : hint != null ? (
        <span id={messageId} className="mt-1 text-xs text-foreground-muted">
          {hint}
        </span>
      ) : null}
    </div>
  );
}
