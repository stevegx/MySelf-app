import { z } from "zod";
import { passwordPolicySchema } from "./passwordPolicySchema";

// Mirrors the backend's UserName charset (Program.cs AllowedUserNameCharacters) and its
// minimum length (AuthEndpoints.cs — Identity has no built-in minimum for UserName).
const USERNAME_PATTERN = /^[a-zA-Z0-9_.-]+$/;

export const registerSchema = z
  .object({
    username: z
      .string()
      .trim()
      .min(3, "Must be at least 3 characters.")
      .max(24, "Must be at most 24 characters.")
      .regex(USERNAME_PATTERN, "Only letters, numbers, dots, underscores and hyphens."),
    email: z.string().trim().min(1, "Email is required.").email("Enter a valid email address."),
    password: passwordPolicySchema,
    confirmPassword: z.string().min(1, "Confirm your password."),
    trustThisDevice: z.boolean(),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "Passwords do not match.",
    path: ["confirmPassword"],
  });

export type RegisterFormValues = z.infer<typeof registerSchema>;
