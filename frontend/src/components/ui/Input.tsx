import type { InputHTMLAttributes, KeyboardEvent } from "react";
import { cn } from "../../lib/cn";

/**
 * The standard text field. For `type="number"` it defaults `min` to 0 and swallows the
 * keys that would produce a negative or scientific value (`-`, `+`, `e`) — so a weight or
 * rep count can never be typed below zero. Pass an explicit `min` to override.
 */
export function Input({ className, type, min, onKeyDown, ...rest }: InputHTMLAttributes<HTMLInputElement>) {
  const isNumber = type === "number";

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (isNumber && (e.key === "-" || e.key === "+" || e.key === "e" || e.key === "E")) {
      e.preventDefault();
      return;
    }
    onKeyDown?.(e);
  };

  return (
    <input
      type={type}
      min={isNumber && min === undefined ? 0 : min}
      onKeyDown={handleKeyDown}
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
