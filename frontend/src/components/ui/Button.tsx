import type { ButtonHTMLAttributes, ReactNode } from "react";
import { cn } from "../../lib/cn";

type Variant = "primary" | "secondary" | "ghost" | "danger";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
  block?: boolean;
  iconOnly?: boolean;
  size?: "sm" | "md";
  children?: ReactNode;
};

const BASE =
  "inline-flex items-center justify-center gap-1.5 rounded-pill font-semibold leading-tight " +
  "cursor-pointer transition-colors disabled:opacity-45 disabled:cursor-not-allowed " +
  "focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";

const VARIANT: Record<Variant, string> = {
  primary: "bg-primary text-on-primary hover:bg-primary-hover active:bg-primary-pressed",
  secondary:
    "border border-border-strong text-foreground hover:bg-surface-subtle active:bg-surface-strong",
  ghost: "text-primary hover:bg-primary-soft",
  danger: "border border-danger/40 text-danger hover:bg-danger-soft",
};

export function Button({
  variant = "secondary",
  block = false,
  iconOnly = false,
  size = "md",
  className,
  type = "button",
  children,
  ...rest
}: ButtonProps) {
  return (
    <button
      type={type}
      className={cn(
        BASE,
        VARIANT[variant],
        size === "md" ? "min-h-11 px-4 py-2 text-sm" : "min-h-9 px-3.5 py-1.5 text-sm",
        variant === "ghost" && !iconOnly && "px-2.5",
        iconOnly && (size === "md" ? "size-11 p-0" : "size-9 p-0"),
        block && "w-full",
        className,
      )}
      {...rest}
    >
      {children}
    </button>
  );
}
