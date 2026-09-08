import type { ReactNode } from "react";

type PageHeaderProps = {
  title: ReactNode;
  subtitle?: ReactNode;
  actions?: ReactNode;
};

export function PageHeader({ title, subtitle, actions }: PageHeaderProps) {
  return (
    <div className="mb-5 flex flex-wrap items-end justify-between gap-3">
      <div>
        {/* The page's single <h1>; size pinned to the design's title scale (h1 base is larger). */}
        <h1 className="mb-0.5 text-[26px]">{title}</h1>
        {subtitle ? (
          <div className="text-sm text-foreground-muted">{subtitle}</div>
        ) : null}
      </div>
      {actions ? <div className="flex flex-wrap gap-2">{actions}</div> : null}
    </div>
  );
}
