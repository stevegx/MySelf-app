import { z } from "zod";
import { passwordPolicySchema } from "./passwordPolicySchema";

export const resetPasswordSchema = z
  .object({
    newPassword: passwordPolicySchema,
    confirmNewPassword: z.string().min(1, "Confirm your new password."),
  })
  .refine((data) => data.newPassword === data.confirmNewPassword, {
    message: "Passwords do not match.",
    path: ["confirmNewPassword"],
  });

export type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>;
