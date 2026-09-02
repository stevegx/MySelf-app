const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

export type FieldErrors = Record<string, string[]>;

/**
 * Thrown for any non-2xx response. Backend errors are RFC 7807 ProblemDetails
 * (title/detail) or ASP.NET's ValidationProblemDetails (title/errors) — this normalizes
 * both so a caller can check `errors` for field-level messages and fall back to `detail`
 * for a single form-level message (e.g. "duplicate email" has no one field to attach to).
 */
export class ApiError extends Error {
  status: number;
  title: string;
  detail?: string;
  errors?: FieldErrors;

  constructor(status: number, title: string, detail?: string, errors?: FieldErrors) {
    super(detail ?? title);
    this.name = "ApiError";
    this.status = status;
    this.title = title;
    this.detail = detail;
    this.errors = errors;
  }
}

type ApiFetchOptions = {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  // The in-memory access token (from useAuth()), for endpoints behind RequireAuthorization().
  // Passed explicitly rather than read from some global — keeps this function pure/testable
  // and makes "this call needs auth" visible at the call site.
  accessToken?: string;
};

export async function apiFetch<T>(path: string, options: ApiFetchOptions = {}): Promise<T> {
  const headers: Record<string, string> = {};
  if (options.body !== undefined) {
    headers["Content-Type"] = "application/json";
  }
  if (options.accessToken) {
    headers.Authorization = `Bearer ${options.accessToken}`;
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: options.method ?? "GET",
    headers,
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    // Lets the browser send/receive the HttpOnly refresh-token cookie across the
    // frontend (5173) <-> API (5242) origin boundary (docs/05).
    credentials: "include",
  });

  const contentType = response.headers.get("content-type") ?? "";
  const isJson = contentType.includes("json");
  const payload = isJson ? await response.json() : undefined;

  if (!response.ok) {
    throw new ApiError(
      response.status,
      payload?.title ?? "Something went wrong",
      payload?.detail,
      payload?.errors,
    );
  }

  return payload as T;
}
