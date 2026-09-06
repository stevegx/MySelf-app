import { useId } from "react";

type Option<T extends string> = { value: T; label: string };

type SegmentedProps<T extends string> = {
  options: Option<T>[];
  value: T;
  onChange: (value: T) => void;
  name?: string;
  "aria-label"?: string;
};

export function Segmented<T extends string>({
  options,
  value,
  onChange,
  name,
  ...rest
}: SegmentedProps<T>) {
  const autoName = useId();
  const groupName = name ?? autoName;

  return (
    <div
      role="group"
      className="inline-flex overflow-hidden rounded-pill border border-border"
      {...rest}
    >
      {options.map((opt) => (
        <label
          key={opt.value}
          className={
            "inline-flex min-h-[38px] cursor-pointer items-center gap-1.5 border-l border-border px-3.5 py-[7px] " +
            "text-[13px] first:border-l-0 hover:bg-surface-subtle " +
            "has-[:checked]:bg-primary has-[:checked]:text-on-primary has-[:checked]:hover:bg-primary " +
            "has-[:focus-visible]:outline has-[:focus-visible]:outline-2 has-[:focus-visible]:-outline-offset-2 has-[:focus-visible]:outline-ring"
          }
        >
          <input
            type="radio"
            name={groupName}
            checked={opt.value === value}
            onChange={() => onChange(opt.value)}
            className="sr-only"
          />
          <span>{opt.label}</span>
        </label>
      ))}
    </div>
  );
}
