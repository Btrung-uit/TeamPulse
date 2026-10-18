---
id: TP-INT-005
type: feature
status: accepted
area: integrations
tags: [teampulse/integrations, excel]
created: 2026-10-14
updated: 2026-10-18
---

# Excel Import Export

TeamPulse dùng ClosedXML để đọc/ghi `.xlsx`. Template tối thiểu gồm `TaskCode`, `Title`, `AssigneeEmail`, `Status`, `Priority`, `DueDate`, `EstimateHours`, `DependsOn`; import có preview/error report, còn export tạo snapshot báo cáo hiện tại.

`TP-040` triển khai hai endpoint dành cho Leader trong project hiện tại:

- `GET /api/workspaces/{workspaceId}/projects/{projectId}/tasks/template` tạo workbook gồm sheet `Tasks` trống có validation và sheet `Example - do not import` chứa dữ liệu mẫu;
- `GET /api/workspaces/{workspaceId}/projects/{projectId}/tasks/export` xuất dữ liệu database theo search, status, priority, assignee, sprint, deadline, open/attention view và sort hiện tại của Task Board.

Ngày được lưu bằng Excel date với format `yyyy-mm-dd`, estimate là số với hai chữ số thập phân và `DependsOn` là danh sách Task Code predecessor. Export không thay đổi dữ liệu và không trở thành source of truth mới.

Related: [[Excel Import Transaction]], [[Excel Source of Truth Rule]].
