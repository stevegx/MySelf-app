import type { InputHTMLAttributes } from "react";
import { cn } from "../../lib/cn";

export function Input({ className, ...rest }: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      className={cn(
        "w-full min-h-11 rounded-control border border-border bg-surface-subtle px-3 py-2 text-sm",
        "text-foreground caret-primary placeholder:text-foreground-subtle",
        "hover:border-border-strong focus-visible:border-primary focus-visible:outline-none",
        "disabled:opacity-60 disabled:cursor-not-allowed",
        className,
      )}
      {...rest}
    />
  );
}
