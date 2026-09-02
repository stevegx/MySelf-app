import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router";
import { Button, Checkbox, Field, Input, PasswordInput } from "../../components/ui";
import { ApiError } from "../../lib/api";
import { useAuth } from "./auth";
import { registerSchema } from "./registerSchema";
import type { RegisterFormValues } from "./registerSchema";
import { useRegister } from "./useRegister";

export function RegisterScreen() {
  const navigate = useNavigate();
  const { setSession } = useAuth();
  const registerMutation = useRegister();
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { trustThisDevice: false },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      const result = await registerMutation.mutateAsync({
        username: values.username,
        email: values.email,
        password: values.password,
        trustThisDevice: values.trustThisDevice,
      });
      setSession({ accessToken: result.accessToken, user: result.user });
      navigate("/dashboard");
    } catch (error) {
      if (error instanceof ApiError && error.errors) {
        // The backend groups every failure by field (username/email/password), with a
        // "form" key for anything that isn't one of those — shown as the banner below.
        const messages: string[] = [];
        for (const [field, fieldMessages] of Object.entries(error.errors)) {
          if (field === "username" || field === "email" || field === "password") {
            setError(field, { message: fieldMessages[0] });
          } else {
            messages.push(...fieldMessages);
          }
        }
        if (messages.length > 0) {
          setFormError(messages.join(" "));
        }
        return;
      }

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
          <h2 className="mb-1">Create your account</h2>
          <p className="m-0 text-sm text-foreground-muted">
            Track workouts and nutrition in one place.
          </p>
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
          label="Username"
          htmlFor="username"
          hint={
            errors.username ? (
              <span className="text-danger">{errors.username.message}</span>
            ) : (
              "3-24 characters: letters, numbers, dots, underscores and hyphens."
            )
          }
        >
          <Input
            id="username"
            type="text"
            autoComplete="username"
            aria-invalid={errors.username ? true : undefined}
            {...register("username")}
          />
        </Field>

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

        <Field
          label="Password"
          htmlFor="password"
          hint={
            errors.password ? (
              <span className="text-danger">{errors.password.message}</span>
            ) : (
              "At least 6 characters, with a number, an uppercase letter and a symbol."
            )
          }
        >
          <PasswordInput
            id="password"
            autoComplete="new-password"
            aria-invalid={errors.password ? true : undefined}
            {...register("password")}
          />
        </Field>

        <Field
          label="Confirm password"
          htmlFor="confirmPassword"
          hint={
            errors.confirmPassword ? (
              <span className="text-danger">{errors.confirmPassword.message}</span>
            ) : undefined
          }
        >
          <PasswordInput
            id="confirmPassword"
            autoComplete="new-password"
            aria-invalid={errors.confirmPassword ? true : undefined}
            {...register("confirmPassword")}
          />
        </Field>

        <Checkbox
          id="trustThisDevice"
          label="Trust this device — stay signed in after closing the browser"
          {...register("trustThisDevice")}
        />

        <Button type="submit" variant="primary" block disabled={isSubmitting}>
          {isSubmitting ? "Creating account…" : "Create account"}
        </Button>

        <p className="m-0 text-center text-sm text-foreground-muted">
          Already have an account?{" "}
          <Link to="/login" className="font-semibold text-primary no-underline hover:underline">
            Log in
          </Link>
        </p>
      </form>
    </div>
  );
}
