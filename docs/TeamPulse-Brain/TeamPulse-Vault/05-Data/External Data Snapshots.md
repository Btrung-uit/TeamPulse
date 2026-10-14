---
id: TP-DATA-006
type: rule
status: proposed
area: data
tags: [teampulse/data, integrations]
created: 2026-10-14
updated: 2026-10-14
---

# External Data Snapshots

TeamPulse chỉ lưu external identifiers, links và metadata cần cho workflow/reporting. Snapshot phải có `LastSyncedAt` và không được giả định là live state; GitHub hoặc external service vẫn là owner của original content.

Retention và refresh policy cần được chốt khi Phase 2 bắt đầu.

Related: [[External Content Ownership]], [[GitHub Collaboration Automation]].
