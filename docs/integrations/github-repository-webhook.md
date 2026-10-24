# GitHub repository webhook

TP-060 provides an authenticated, durable inbox for GitHub repository events. It verifies every payload before storing it, deduplicates retries by `X-GitHub-Delivery`, records processing state for audit, and processes accepted deliveries outside the HTTP request. TP-061 interprets accepted commit and pull-request events to create evidence and apply the `Task → Evidence → Review` rules.

## Map a repository to a project

In the project's Resource Hub, create a Resource with:

| Field | Value |
| --- | --- |
| Type | `GitHub` |
| External URL | Repository URL, for example `https://github.com/Btrung-uit/TeamPulse` |
| Repository / External ID | Exact full name in `owner/repository` form, for example `Btrung-uit/TeamPulse` |

Repository matching is case-insensitive. One repository may be mapped to multiple projects, but only matching task codes inside each mapped project are changed. A valid delivery with no matching Resource or task code is still processed successfully and makes no task changes.

## Supported events

Configure the repository webhook for these event groups:

- Pushes.
- Pull requests.
- Pull request reviews.

GitHub's automatic `ping` event is authenticated and audited as `Ignored`. Unsupported events/actions are also audited without entering background processing.

## Local configuration

Generate a secret with at least 32 UTF-8 bytes and keep it outside the repository:

```powershell
$webhookSecret = [Convert]::ToBase64String(
  [Security.Cryptography.RandomNumberGenerator]::GetBytes(48))

dotnet user-secrets set --project src/backend/TeamPulse.Api `
  "Integrations:GitHub:Webhook:Enabled" true
dotnet user-secrets set --project src/backend/TeamPulse.Api `
  "Integrations:GitHub:Webhook:Secret" $webhookSecret
```

For the Docker Compose stack, set the same values in the ignored `.env` file:

```dotenv
GITHUB_WEBHOOK_ENABLED=true
GITHUB_WEBHOOK_SECRET=<same-random-secret>
```

GitHub cannot deliver to `localhost`. Use an HTTPS deployment or an HTTPS tunnel intended for development, and remove the tunnel when testing ends.

## GitHub repository settings

Open **Repository → Settings → Webhooks → Add webhook** and configure:

| Setting | Value |
| --- | --- |
| Payload URL | `https://<team-pulse-host>/api/integrations/github/webhook` |
| Content type | `application/json` |
| Secret | The exact server-side secret configured above |
| SSL verification | Enabled |
| Events | Pushes, Pull requests, Pull request reviews |
| Active | Enabled |

Do not put the secret in frontend environment variables, source control, screenshots, issue descriptions, or logs.

## Delivery behavior

| Condition | HTTP result | Audit behavior |
| --- | --- | --- |
| Valid supported delivery | `202 Accepted` | Stored as `Received`, then processed by the background worker |
| Valid replayed delivery ID | `202 Accepted` | Existing record retained; no second delivery is created |
| Valid unsupported event/action | `202 Accepted` | Stored once as `Ignored` |
| Missing or invalid signature | `401 Unauthorized` | Not stored |
| Invalid headers or JSON | `400 Bad Request` | Not stored |
| Payload above configured limit | `413 Payload Too Large` | Not stored |
| Integration disabled | `503 Service Unavailable` | Not stored |

The default payload limit is 2 MiB and may be configured with `Integrations__GitHub__Webhook__MaximumPayloadBytes`, up to 25 MiB.

## Durable processing and diagnostics

`GitHubWebhookDeliveries` is the durable inbox and audit trail. A unique index on `DeliveryId` protects against concurrent retries. Each record keeps event/action, repository identity, payload hash, processing status, attempt count, timestamps, and the last error.

The background worker retries abandoned `Processing` records after a five-minute lease. Evidence, task transitions, activities, and the final delivery state are saved together; a handler failure clears pending entity changes before the delivery is marked failed.

## Evidence and task automation

Reference a task code such as `TP-123` in a commit message or PR title/body.

| GitHub event | TeamPulse result |
| --- | --- |
| Commit in a push | Adds commit evidence; `In Progress` may advance to `Evidence Linked`; never marks the task `Done` |
| PR opened/reopened as non-draft, or ready for review | Adds PR evidence and advances through legal states to `In Review` |
| Review submitted with changes requested | Moves `In Review` to `Needs Changes` |
| PR merged | Advances through legal states to `Done` |
| PR closed without merge | Returns evidence/review/changes states to `In Progress` |

Commit SHA and PR number provide idempotent evidence identities. Malformed task codes are ignored. Automation does not overwrite `Blocked` or already `Done` tasks.

Automated evidence and activities are attributed to `GitHubAutomation`, not to a workspace member. Each automated activity stores the source delivery ID for traceability. Leaders can still change status manually; that override creates the normal user-attributed task activity and remains distinguishable from automation.

The current public demo runs as one API instance. Before scaling to multiple API instances, replace the read-then-lease step with an atomic database claim so two workers cannot select the same pending delivery. Define a payload retention policy before enabling the integration for high-volume repositories.

When troubleshooting a GitHub delivery:

1. Match GitHub's delivery ID to `GitHubWebhookDeliveries.DeliveryId` and structured API logs.
2. Check `Status`, `AttemptCount`, `LastAttemptedAtUtc`, and `Error`.
3. A `401` usually means the repository and server secrets differ.
4. A missing audit row with no GitHub response usually indicates routing, TLS, or deployment configuration before TeamPulse received the request.
