---
id: TP-INT-002
type: integration
status: accepted
area: integrations
tags: [teampulse/integrations, github, security]
created: 2026-10-14
updated: 2026-10-14
---

# GitHub Webhook Security

Webhook endpoint xác thực `X-Hub-Signature-256`, deduplicate `X-GitHub-Delivery`, kiểm tra event/action, phản hồi nhanh và xử lý nặng bất đồng bộ. Secret/token chỉ lưu server-side, mã hóa khi persistence và cấp minimum GitHub App permissions.

TP-060 chọn repository webhook. Implementation dùng durable inbox `GitHubWebhookDeliveries`: chỉ payload có HMAC-SHA256 hợp lệ mới được lưu; `DeliveryId` có unique index; event/action ngoài allowlist được audit ở trạng thái `Ignored`; worker xử lý bản ghi `Received` ngoài HTTP request và nhận lại processing lease bị bỏ dở sau năm phút. Secret lấy từ server configuration, không persistence trong application database.

TP-061 consumes accepted deliveries to create evidence and transition tasks. Automation keeps GitHub as a distinct system actor and records the delivery ID in its activity audit trail.

Runbook: [`docs/integrations/github-repository-webhook.md`](../../integrations/github-repository-webhook.md).

Related: [[GitHub Collaboration Automation]], [[Identity and Authorization]].
