# Security and reliability checklist

This checklist records the TP-051 API boundary review. It complements the automated integration tests and should be revisited before each public deployment.

## Implemented and regression-tested

- [x] Authentication endpoints (`register`, `login`, and `refresh`) have a fixed-window rate limit.
- [x] Excel upload, preview, and confirm endpoints have a separate fixed-window rate limit.
- [x] Rate-limit responses use HTTP `429` and the stable `rate_limited` error code.
- [x] CORS accepts only explicitly configured origins and rejects wildcard origins when credentials are enabled.
- [x] Protected workspace, project, task, dashboard, resource, planning, and import routes require authentication.
- [x] Leader-only mutations return `403` for authenticated members; cross-workspace access is covered by tenant-boundary tests.
- [x] Excel imports accept only `.xlsx`, stop reading above 5 MB, reject malformed workbooks, and cap previews at 2,000 data rows.
- [x] Unhandled exceptions return a generic Problem Details response with a trace ID; exception messages and stack traces stay server-side.
- [x] Every response includes `X-Correlation-ID`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, and `Referrer-Policy: no-referrer`.
- [x] Request logs contain method, path, status, duration, correlation ID, and trace ID without logging query strings, request bodies, tokens, or uploaded data.

## Configuration

Production must provide an explicit frontend allowlist. Array values use indexed environment variables:

```text
Cors__AllowedOrigins__0=https://app.example.com
RateLimiting__Auth__PermitLimit=30
RateLimiting__Import__PermitLimit=10
RateLimiting__WindowSeconds=60
```

An empty CORS allowlist is safe for the local Vite proxy and other same-origin clients. Rate limits are per authenticated user when a subject claim is present, otherwise per remote IP address.

## Deployment checks still required

- [ ] Terminate HTTPS at the application host or trusted reverse proxy and configure forwarded headers for the real client IP.
- [ ] Store JWT and database secrets in the deployment secret store; rotate any value ever committed or shared.
- [ ] Restrict database credentials to the application database and least required permissions.
- [ ] Add a deployment-specific Content Security Policy after the final asset/API origins are known.
- [ ] Send structured logs to a protected sink with retention, redaction, and alerting rules.
- [ ] Reassess rate-limit values using production traffic and move counters to a distributed limiter before horizontal scaling.
