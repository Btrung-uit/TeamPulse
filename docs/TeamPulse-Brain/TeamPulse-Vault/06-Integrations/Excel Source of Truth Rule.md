---
id: TP-INT-008
type: decision
status: accepted
area: integrations
tags: [teampulse/integrations, excel, decision]
created: 2026-10-14
updated: 2026-10-14
---

# Excel Source of Truth Rule

Excel dùng để khởi tạo và export; sau confirm import, TeamPulse database là source of truth. Re-upload không tự overwrite thay đổi trên web; nếu sau này hỗ trợ upsert, nó phải có stable Task Code, dry-run và conflict review.

Canonical decision: `docs/adr/0003-make-teampulse-the-source-of-truth-after-import.md`.

Related: [[Import Batch]], [[Excel Import Export]].
