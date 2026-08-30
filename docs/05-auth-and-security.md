# MySelf App — 05 Auth And Security

> Split documentation file. Purpose: Authentication, token/session handling, authorization, CORS, secrets and security constraints.
> Original source: `myself-app-blueprint-v1.1-learning-r1.md`

## 13. Authentication and security

- ASP.NET Core Identity handles users and password hashing.
- Short-lived access token kept in memory.
- Rotating refresh token in `HttpOnly`, `Secure`, appropriate `SameSite` cookie.
- No auth token in `localStorage`.
- Email normalization, lockout/rate limiting on auth endpoints.
- Strict CORS allow-list, HTTPS, security headers and request-size limits.
- Authorization checks by owner on every user resource.
- External API keys only in backend secrets/environment variables.
- Sensitive logs must not contain passwords, tokens, full meal history or health notes.
- User can export and delete their data.

Για local learning environment, email verification/reset μπορεί αρχικά να γράφει dev link σε safe local sink. Πριν από public deployment χρειάζεται πραγματικός email provider.
