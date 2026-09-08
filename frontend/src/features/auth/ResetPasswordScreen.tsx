import { useState } from "react";
import type { ReactNode } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { Link, useSearchParams } from "react-router";
import { Button, Field, PasswordInput } from "../../components/ui";
import { ApiError } from "../../lib/api";
import { resetPasswordSchema } from "./resetPasswordSchema";
import type { ResetPasswordFormValues } from "./resetPasswordSchema";
import { useResetPassword } from "./useResetPassword";

function Card({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-5 py-12">
      <div className="flex w-[min(420px,100%)] flex-col gap-5 rounded-card border border-border bg-surface p-9 pb-7 shadow-md">
        {children}
      </div>
    </div>
  );
}

export function ResetPasswordScreen() {
  const [searchParams] = useSearchParams();
  const email = searchParams.get("email");
  const token = searchParams.get("token");

  const resetPasswordMutation = useResetPassword();
  const [formError, setFormError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ResetPasswordFormValues>({ resolver: zodResolver(resetPasswordSchema) });

  if (!email || !token) {
    return (
      <Card>
        <h2 className="mb-1">Invalid reset link</h2>
        <p className="m-0 text-sm text-foreground-muted">
          This link is missing information it needs. Request a new one.
        </p>
        <Link
          to="/forgot-password"
          className="self-start font-semibold text-primary no-underline hover:underline"
        >
          Request a new link
        </Link>
      </Card>
    );
  }

  if (done) {
    return (
      <Card>
        <h2 className="mb-1">Password reset</h2>
        <p className="m-0 text-sm text-foreground-muted">
          Your password has been changed. Other signed-in devices have been logged out for
          your security.
        </p>
        <Link to="/login" className="self-start font-semibold text-primary no-underline hover:underline">
          Log in
        </Link>
      </Card>
    );
  }

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      await resetPasswordMutation.mutateAsync({ email, token, newPassword: values.newPassword });
      setDone(true);
    } catch (error) {
      if (error instanceof ApiError && error.errors?.newPassword) {
        setError("newPassword", { message: error.errors.newPassword[0] });
        return;
      }

      setFormError(
        error instanceof ApiError ? (error.detail ?? error.title) : "Something went wrong. Please try again.",
      );
    }
  });

  return (
    <Card>
      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-5">
        <div>
          <h2 className="mb-1">Choose a new password</h2>
          <p className="m-0 text-sm text-foreground-muted">for {email}</p>
        </div>

        {formError ? (
          <div
            role="alert"
            className="rounded-control border border-danger/40 bg-danger-soft px-3 py-2 text-sm text-danger"
          >
            {formError}
          </div>
        ) : null}

        <Field
          label="New password"
          htmlFor="newPassword"
          hint="At least 6 characters, with a number, an uppercase letter and a symbol."
          error={errors.newPassword?.message}
        >
          <PasswordInput
            id="newPassword"
            autoComplete="new-password"
            aria-invalid={errors.newPassword ? true : undefined}
            {...register("newPassword")}
          />
        </Field>

        <Field
          label="Confirm new password"
          htmlFor="confirmNewPassword"
          error={errors.confirmNewPassword?.message}
        >
          <PasswordInput
            id="confirmNewPassword"
            autoComplete="new-password"
            aria-invalid={errors.confirmNewPassword ? true : undefined}
            {...register("confirmNewPassword")}
          />
        </Field>

        <Button type="submit" variant="primary" block disabled={isSubmitting}>
          {isSubmitting ? "Resetting…" : "Reset password"}
        </Button>
      </form>
    </Card>
  );
}
