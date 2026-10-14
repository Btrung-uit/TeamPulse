# Demo seed data

`TeamPulse-Demo-Tasks.xlsx` is the import-ready workbook used by the CampusEvent demonstration scenario in [`docs/demo/TP-001-demo-scenario.md`](../../docs/demo/TP-001-demo-scenario.md).

It has one sheet named `Tasks` and uses the MVP import columns:

`TaskCode`, `Title`, `AssigneeEmail`, `Status`, `Priority`, `DueDate`, `EstimateHours`, `DependsOn`.

Generate it again after changing the demo data:

```powershell
node scripts/generate-demo-workbook.mjs
```
