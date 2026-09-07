import type { HTMLAttributes, ReactNode } from "react";
import { cn } from "../../lib/cn";

type CardProps = HTMLAttributes<HTMLDivElement> & {
  elevated?: boolean;
  children?: ReactNode;
};

export function Card({ elevated = true, className, children, ...rest }: CardProps) {
  return (
    <div
      className={cn(
        "flex flex-col gap-2 p-4 rounded-card bg-surface border border-border",
        elevated && "shadow-sm",
        className,
      )}
      {...rest}
    >
      {children}
    </div>
  );
}

export function CardKicker({ children }: { children: ReactNode }) {
  return (
    <div className="text-[11px] tracking-[0.1em] uppercase text-primary">{children}</div>
  );
}

export function CardTitle({ children }: { children: ReactNode }) {
  return (
    <div className="font-[family-name:var(--font-display)] text-[19px] font-normal leading-tight">
      {children}
    </div>
  );
}

export function CardBody({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <p className={cn("m-0 text-sm text-foreground-muted flex-1", className)}>{children}</p>
  );
}
