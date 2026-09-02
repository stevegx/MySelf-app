import { z } from "zod";

// Mirrors the backend's Identity password policy (AddIdentityCore defaults, see
// backend/MySelf.Api/Program.cs): min 6 chars, at least one digit/lowercase/uppercase/
// non-alphanumeric. Shared by register and reset-password since both create a new
// password under the same rules. Client-side checks give instant feedback (Story 1:
// "password rules appear before submit"); the backend remains the actual authority.
export const passwordPolicySchema = z
  .string()
  .min(6, "Must be at least 6 characters.")
  .regex(/[0-9]/, "Must include a number.")
  .regex(/[a-z]/, "Must include a lowercase letter.")
  .regex(/[A-Z]/, "Must include an uppercase letter.")
  .regex(/[^a-zA-Z0-9]/, "Must include a symbol.");
