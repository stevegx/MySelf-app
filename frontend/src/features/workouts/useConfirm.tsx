import { useCallback, useRef, useState } from "react";
import { Button, Modal } from "../../components/ui";

type ConfirmOptions = {
  title: string;
  message: string;
  confirmLabel?: string;
  tone?: "danger" | "primary";
};

/**
 * A small promise-based replacement for window.confirm: styled, theme-aware, keyboard
 * dismissable, and non-blocking. `confirm(opts)` resolves true/false; render `dialog`
 * once somewhere in the tree.
 */
export function useConfirm() {
  const [options, setOptions] = useState<ConfirmOptions | null>(null);
  const resolver = useRef<((ok: boolean) => void) | null>(null);

  const confirm = useCallback((opts: ConfirmOptions) => {
    setOptions(opts);
    return new Promise<boolean>((resolve) => {
      resolver.current = resolve;
    });
  }, []);

  const settle = (ok: boolean) => {
    resolver.current?.(ok);
    resolver.current = null;
    setOptions(null);
  };

  const dialog = options ? (
    <Modal title={options.title} onClose={() => settle(false)}>
      <p className="m-0 text-sm text-foreground-muted">{options.message}</p>
      <div className="flex justify-end gap-2">
        <Button variant="ghost" size="sm" onClick={() => settle(false)}>
          Cancel
        </Button>
        <Button
          variant={options.tone === "primary" ? "primary" : "danger"}
          size="sm"
          autoFocus
          onClick={() => settle(true)}
        >
          {options.confirmLabel ?? "Confirm"}
        </Button>
      </div>
    </Modal>
  ) : null;

  return { confirm, dialog };
}
