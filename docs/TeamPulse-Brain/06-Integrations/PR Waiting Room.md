---
id: TP-INT-003
type: feature
status: accepted
area: integrations
tags: [teampulse/integrations, github, review]
created: 2026-10-14
updated: 2026-10-24
---

# PR Waiting Room

PR Waiting Room tập trung PR cần review, task liên quan, author, assigned reviewers, checks và thời gian chờ. TeamPulse thông báo đúng reviewer và deep-link sang GitHub; code review và merge vẫn diễn ra trên GitHub.

TP-062 implements the screen as a durable projection of authenticated repository webhook deliveries. It groups `Draft`, `Waiting for review`, and `Ready to merge` under Waiting; exposes separate Changes and Merged filters; and stores links to every task code found in the PR title/body. Completed check-suite webhooks provide the aggregate checks result.

The frontend polls every 30 seconds. This deliberately avoids adding SignalR before the queue semantics and notification signal-to-noise are proven. Notifications cover ready-for-review, changes-requested, failing-checks, and merged events and open the exact GitHub PR; review and merge actions never occur inside TeamPulse.

Related: [[Evidence and Review]], [[External Content Ownership]], [[Notification]].
