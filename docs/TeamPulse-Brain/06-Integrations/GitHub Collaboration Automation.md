---
id: TP-INT-001
type: integration
status: accepted
area: integrations
tags: [teampulse/integrations, github]
created: 2026-10-14
updated: 2026-10-24
---

# GitHub Collaboration Automation

Commit chứa `TP-123` tự gắn evidence nhưng không Done task. PR ready đưa task sang In Review; review changes requested đưa sang Needs Changes; merged PR đưa sang Done; closed-unmerged đưa về In Progress. Leader luôn có manual override và automation phải ghi Activity Log.

TP-061 implements this policy on top of the TP-060 durable webhook inbox. A project opts in by adding a GitHub Resource whose `ExternalId` is the case-insensitive repository full name (`owner/repository`). Commit SHA and PR number are stable evidence identities, so GitHub retries or later PR events do not duplicate evidence.

Automated evidence has no human submitter and is marked `GitHubAutomation`. Automated activity likewise has no human actor, stores the GitHub delivery ID as `SourceReference`, and is excluded from individual contribution counts. A leader's later manual transition uses the existing task API and remains a user-attributed activity; automation does not lock the task.

Guardrails: commit events may advance `In Progress` to `Evidence Linked`, but never to `Done`; malformed task codes are ignored; `Blocked` and `Done` tasks are not automatically overridden. PR lifecycle transitions still use the domain's legal transition path.

Related: [[Task Lifecycle]], [[PR Waiting Room]], [[GitHub Webhook Security]].
