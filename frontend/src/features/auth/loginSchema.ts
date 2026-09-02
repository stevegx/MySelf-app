import { z } from "zod";

export const loginSchema = z.object({
  identifier: z.string().trim().min(1, "Enter your username or email."),
  password: z.string().min(1, "Password is required."),
  trustThisDevice: z.boolean(),
});

export type LoginFormValues = z.infer<typeof loginSchema>;
