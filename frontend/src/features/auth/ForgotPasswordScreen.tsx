import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { Link } from "react-router";
import { Button, Field, Input } from "../../components/ui";
import { ApiError } from "../../lib/api";
import { forgotPasswordSchema } from "./forgotPasswordSchema";
import type { ForgotPasswordFormValues } from "./forgotPasswordSchema";
import { useForgotPassword } from "./useForgotPassword";

export function ForgotPasswordScreen() {
  const forgotPasswordMutation = useForgotPassword();
  const [formError, setFormError] = useState<string | null>(null);
  const [result, setResult] = useState<{ message: string; resetLink: string | null } | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ForgotPasswordFormValues>({ resolver: zodResolver(forgotPasswordSchema) });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      setResult(await forgotPasswordMutation.mutateAsync(values));
    } catch (error) {
      setFormError(
        error instanceof ApiError ? (error.detail ?? error.title) : "Something went wrong. Please try again.",
      );
    }
  });

  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-5 py-12">
      <div className="flex w-[min(420px,100%)] flex-col gap-5 rounded-card border border-border bg-surface p-9 pb-7 shadow-md">
        <div>
          <h2 className="mb-1">Reset your password</h2>
          <p className="m-0 text-sm text-foreground-muted">
            Enter your email and we'll send a reset link if an account exists for it.
          </p>
        </div>

        {result ? (
          <div className="flex flex-col gap-3">
            <div
              role="status"
              className="rounded-control border border-border bg-surface-subtle px-3 py-2 text-sm text-foreground"
            >
              {result.message}
            </div>
            {result.resetLink ? (
              <div className="rounded-control border border-primary/40 bg-primary-soft px-3 py-2 text-xs">
                <div className="mb-1 font-semibold text-foreground">
                  Dev mode — no real email is sent, use this link directly:
                </div>
                <a href={result.resetLink} className="break-all text-primary underline">
                  {result.resetLink}
                </a>
              </div>
            ) : null}
          </div>
        ) : (
          <form onSubmit={onSubmit} noValidate className="flex flex-col gap-5">
            {formError ? (
              <div
                role="alert"
                className="rounded-control border border-danger/40 bg-danger-soft px-3 py-2 text-sm text-danger"
              >
                {formError}
              </div>
            ) : null}

            <Field
              label="Email"
              htmlFor="email"
              hint={errors.email ? <span className="text-danger">{errors.email.message}</span> : undefined}
            >
              <Input
                id="email"
                type="email"
                autoComplete="email"
                aria-invalid={errors.email ? true : undefined}
                {...register("email")}
              />
            </Field>

            <Button type="submit" variant="primary" block disabled={isSubmitting}>
              {isSubmitting ? "Sending…" : "Send reset link"}
            </Button>
          </form>
        )}

        <p className="m-0 text-center text-sm text-foreground-muted">
          <Link to="/login" className="font-semibold text-primary no-underline hover:underline">
            Back to log in
          </Link>
        </p>
      </div>
    </div>
  );
}
