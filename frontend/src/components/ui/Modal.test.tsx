import { useState } from "react";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Modal } from "./Modal";

function Harness({ label }: { label?: string }) {
  const [open, setOpen] = useState(false);
  return (
    <div>
      <button type="button" onClick={() => setOpen(true)}>
        Open
      </button>
      {open && (
        <Modal title="Rename day" label={label} onClose={() => setOpen(false)}>
          <input aria-label="Name" defaultValue="Push A" />
          <button type="button" onClick={() => setOpen(false)}>
            Save
          </button>
        </Modal>
      )}
    </div>
  );
}

describe("Modal", () => {
  it("names itself from the visible title and moves focus inside on open", async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole("button", { name: "Open" }));

    const dialog = screen.getByRole("dialog", { name: "Rename day" });
    expect(dialog).toBeInTheDocument();
    expect(screen.getByLabelText("Name")).toHaveFocus();
  });

  it("can take an explicit accessible name that differs from the heading", async () => {
    const user = userEvent.setup();
    render(<Harness label="Rename the day Push A" />);

    await user.click(screen.getByRole("button", { name: "Open" }));
    expect(screen.getByRole("dialog", { name: "Rename the day Push A" })).toBeInTheDocument();
    // The visible heading is still the title.
    expect(screen.getByRole("heading", { name: "Rename day" })).toBeInTheDocument();
  });

  it("traps Tab within the dialog", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByRole("button", { name: "Open" }));

    const name = screen.getByLabelText("Name");
    const save = screen.getByRole("button", { name: "Save" });

    expect(name).toHaveFocus();
    await user.tab();
    expect(save).toHaveFocus();
    await user.tab(); // wraps back to the first focusable
    expect(name).toHaveFocus();
    await user.tab({ shift: true }); // wraps to the last
    expect(save).toHaveFocus();
  });

  it("closes on Escape and restores focus to the trigger", async () => {
    const user = userEvent.setup();
    render(<Harness />);

    const open = screen.getByRole("button", { name: "Open" });
    await user.click(open);
    expect(screen.getByRole("dialog")).toBeInTheDocument();

    await user.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(open).toHaveFocus();
  });

  it("locks body scroll while open and releases it on close", async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole("button", { name: "Open" }));
    expect(document.body.style.overflow).toBe("hidden");

    await user.keyboard("{Escape}");
    expect(document.body.style.overflow).toBe("");
  });
});
