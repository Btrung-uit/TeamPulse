---
id: TP-INT-005
type: feature
status: accepted
area: integrations
tags: [teampulse/integrations, excel]
created: 2026-10-14
updated: 2026-10-14
---

# Excel Import Export

TeamPulse dùng ClosedXML để đọc/ghi `.xlsx`. Template tối thiểu gồm `TaskCode`, `Title`, `AssigneeEmail`, `Status`, `Priority`, `DueDate`, `EstimateHours`, `DependsOn`; import có preview/error report, còn export tạo snapshot báo cáo hiện tại.

Related: [[Excel Import Transaction]], [[Excel Source of Truth Rule]].
