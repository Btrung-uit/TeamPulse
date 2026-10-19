# Scripts

Repository-level developer and CI helpers:

- `New-DeploymentEnv.ps1`: create a local `.env` with random deployment secrets.
- `Seed-DemoScenario.ps1`: reproducibly seed the four-person CampusEvent demo through the public API.
- `generate-demo-workbook.mjs`: regenerate the typed `.xlsx` task source.

Run scripts from the repository root so their documented relative paths remain
easy to copy. The seed script resolves its workbook relative to its own file and
can therefore also be invoked from another working directory.
