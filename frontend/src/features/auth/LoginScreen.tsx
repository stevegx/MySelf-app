import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router";
import { Button, Checkbox, Field, Input, PasswordInput } from "../../components/ui";
import { ApiError } from "../../lib/api";
import { useAuth } from "./auth";
import { loginSchema } from "./loginSchema";
import type { LoginFormValues } from "./loginSchema";
import { useLogin } from "./useLogin";

export function LoginScreen() {
  const navigate = useNavigate();
  const { setSession } = useAuth();
  const loginMutation = useLogin();
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { trustThisDevice: false },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      const result = await loginMutation.mutateAsync(values);
      setSession({ accessToken: result.accessToken, user: result.user });
      navigate("/dashboard");
    } catch (error) {
      // Login never attaches an error to a specific field (docs/05: don't reveal whether
      // it was the identifier or the password that was wrong) — always a form-level banner.
      setFormError(
        error instanceof ApiError ? (error.detail ?? error.title) : "Something went wrong. Please try again.",
      );
    }
  });

  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-5 py-12">
      <form
        onSubmit={onSubmit}
        noValidate
        className="flex w-[min(420px,100%)] flex-col gap-5 rounded-card border border-border bg-surface p-9 pb-7 shadow-md"
      >
        <div>
          <h2 className="mb-1">Welcome back</h2>
          <p className="m-0 text-sm text-foreground-muted">Log in to continue.</p>
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
          label="Username or email"
          htmlFor="identifier"
          error={errors.identifier?.message}
        >
          <Input
            id="identifier"
            type="text"
            autoComplete="username"
            aria-invalid={errors.identifier ? true : undefined}
            {...register("identifier")}
          />
        </Field>

        <Field
          label="Password"
          htmlFor="password"
          error={errors.password?.message}
        >
          <PasswordInput
            id="password"
            autoComplete="current-password"
            aria-invalid={errors.password ? true : undefined}
            {...register("password")}
          />
        </Field>

        <Link
          to="/forgot-password"
          className="-mt-3 self-end text-sm font-semibold text-primary no-underline hover:underline"
        >
          Forgot password?
        </Link>

        <Checkbox
          id="trustThisDevice"
          label="Trust this device — stay signed in after closing the browser"
          {...register("trustThisDevice")}
        />

        <Button type="submit" variant="primary" block disabled={isSubmitting}>
          {isSubmitting ? "Logging in…" : "Log in"}
        </Button>

        <p className="m-0 text-center text-sm text-foreground-muted">
          Don't have an account?{" "}
          <Link to="/register" className="font-semibold text-primary no-underline hover:underline">
            Create one
          </Link>
        </p>
      </form>
    </div>
  );
}
