# Demo seed data

`TeamPulse-Demo-Tasks.xlsx` is the import-ready workbook used by the CampusEvent demonstration scenario in [`docs/demo/TP-001-demo-scenario.md`](../../docs/demo/TP-001-demo-scenario.md).

It has one sheet named `Tasks` and uses the MVP import columns:

`TaskCode`, `Title`, `AssigneeEmail`, `Status`, `Priority`, `DueDate`, `EstimateHours`, `DependsOn`.

Generate it again after changing the demo data:

```powershell
node scripts/generate-demo-workbook.mjs
```

Create the complete API-backed portfolio scenario after the app is running:

```powershell
.\scripts\Seed-DemoScenario.ps1
```

The script is idempotent. It registers or signs in the four demo users, creates
their workspace/project when absent, imports exactly 20 tasks transactionally,
then adds evidence, an accepted review, and resource links. A partial `CE-*`
task set is rejected instead of silently creating duplicates.

The default credentials are local demo data only. Override the password with
`-DemoPassword` if the environment is reachable by anyone else.
