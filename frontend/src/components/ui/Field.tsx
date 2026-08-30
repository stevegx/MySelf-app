import type { ReactNode } from "react";
import { cn } from "../../lib/cn";

type FieldProps = {
  label: ReactNode;
  htmlFor?: string;
  hint?: ReactNode;
  className?: string;
  children: ReactNode;
};

export function Field({ label, htmlFor, hint, className, children }: FieldProps) {
  return (
    <div className={cn("flex flex-col", className)}>
      <label htmlFor={htmlFor} className="mb-1.5 text-xs text-foreground-muted">
        {label}
      </label>
      {children}
      {hint ? <span className="mt-1 text-xs text-foreground-muted">{hint}</span> : null}
    </div>
  );
}
