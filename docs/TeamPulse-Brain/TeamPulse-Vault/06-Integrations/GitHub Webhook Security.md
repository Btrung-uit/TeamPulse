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

Related: [[GitHub Collaboration Automation]], [[Identity and Authorization]].
