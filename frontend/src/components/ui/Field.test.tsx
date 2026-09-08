import { render, screen } from "@testing-library/react";
import { Field } from "./Field";
import { Input } from "./Input";

describe("Field", () => {
  it("associates the label with the control", () => {
    render(
      <Field label="Weight (kg)" htmlFor="w">
        <Input id="w" />
      </Field>,
    );
    expect(screen.getByLabelText("Weight (kg)")).toBeInTheDocument();
  });

  it("wires a hint to the control via aria-describedby", () => {
    render(
      <Field label="Username" htmlFor="u" hint="3–24 characters.">
        <Input id="u" />
      </Field>,
    );
    const input = screen.getByLabelText("Username");
    const describedBy = input.getAttribute("aria-describedby");
    expect(describedBy).toBeTruthy();
    expect(document.getElementById(describedBy!)).toHaveTextContent("3–24 characters.");
    expect(input).not.toHaveAttribute("aria-invalid");
  });

  it("shows the error instead of the hint, marks the control invalid, and announces it", () => {
    render(
      <Field label="Email" htmlFor="e" hint="We'll never share it." error="Enter a valid email.">
        <Input id="e" />
      </Field>,
    );
    const input = screen.getByLabelText("Email");
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(screen.queryByText("We'll never share it.")).not.toBeInTheDocument();

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent("Enter a valid email.");
    expect(input.getAttribute("aria-describedby")).toContain(alert.id);
  });
});
