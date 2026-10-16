---
status: accepted
---

# Use short-lived JWT access tokens and rotating refresh sessions

TeamPulse uses ASP.NET Core Identity for password hashing, account lockout, and
credential persistence. The API issues a 15-minute JWT access token to the web
client and keeps it only in browser memory. A seven-day opaque refresh token is
sent in an `HttpOnly`, `SameSite=Strict` cookie; only its SHA-256 hash is stored
in SQL Server.

Every refresh rotates the opaque token and revokes the previous session row.
Reusing an already revoked token revokes the user's remaining active sessions.
Logout revokes the current refresh session and deletes the cookie.

Workspace roles are deliberately excluded from JWT claims. Authorization must
load current membership and resource ownership from the database so a stale
token cannot preserve removed workspace access.

This first-party session design is appropriate for the TeamPulse portfolio MVP.
If TeamPulse later serves third-party clients or needs federation, replace token
issuance with a standards-based OAuth/OIDC provider rather than expanding this
custom issuer.
