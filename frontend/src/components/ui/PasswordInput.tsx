import { forwardRef, useState } from "react";
import type { InputHTMLAttributes } from "react";
import { Eye, EyeOff } from "lucide-react";
import { cn } from "../../lib/cn";

type PasswordInputProps = Omit<InputHTMLAttributes<HTMLInputElement>, "type">;

/** An Input with a show/hide toggle. Forwards its ref so it works with `{...register()}`. */
export const PasswordInput = forwardRef<HTMLInputElement, PasswordInputProps>(
  function PasswordInput({ className, ...rest }, ref) {
    const [visible, setVisible] = useState(false);

    return (
      <div className="relative">
        <input
          ref={ref}
          type={visible ? "text" : "password"}
          className={cn(
            "w-full min-h-11 rounded-control border border-border bg-surface-subtle py-2 pr-10 pl-3 text-sm",
            "text-foreground caret-primary placeholder:text-foreground-subtle",
            "hover:border-border-strong focus-visible:border-primary focus-visible:outline-none",
            "disabled:opacity-60 disabled:cursor-not-allowed",
            className,
          )}
          {...rest}
        />
        <button
          type="button"
          onClick={() => setVisible((current) => !current)}
          tabIndex={-1}
          aria-label={visible ? "Hide password" : "Show password"}
          className="absolute right-1 top-1/2 flex size-8 -translate-y-1/2 items-center justify-center rounded-control text-foreground-muted hover:text-foreground"
        >
          {visible ? <EyeOff size={16} aria-hidden /> : <Eye size={16} aria-hidden />}
        </button>
      </div>
    );
  },
);
