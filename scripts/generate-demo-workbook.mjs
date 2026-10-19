import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const outputPath = path.resolve(scriptDir, "..", "database", "seed", "TeamPulse-Demo-Tasks.xlsx");

// Artifact Tool currently exports JavaScript Date values as ISO `t="d"` cells.
// ClosedXML cannot parse that representation when it includes milliseconds and
// a UTC suffix, so store Excel's numeric date serial and format it as a date.
const excelDate = (isoDate) => {
  const [year, month, day] = isoDate.split("-").map(Number);
  return (Date.UTC(year, month - 1, day) - Date.UTC(1899, 11, 30)) / 86_400_000;
};

const tasks = [
  ["CE-101", "Define project scope and success criteria", "bao.trung@teampulse.demo", "Done", "High", excelDate("2026-10-06"), 3, ""],
  ["CE-102", "Create wireframes for key screens", "minh.anh@teampulse.demo", "Done", "High", excelDate("2026-10-07"), 5, "CE-101"],
  ["CE-103", "Set up repository and branch rules", "bao.trung@teampulse.demo", "Done", "Medium", excelDate("2026-10-07"), 2, "CE-101"],
  ["CE-104", "Design SQL Server event schema", "thu.ha@teampulse.demo", "Done", "High", excelDate("2026-10-08"), 4, "CE-101"],
  ["CE-105", "Create API solution structure", "gia.huy@teampulse.demo", "Done", "High", excelDate("2026-10-08"), 4, "CE-103"],
  ["CE-106", "Implement authentication endpoints", "gia.huy@teampulse.demo", "In Review", "High", excelDate("2026-10-12"), 8, "CE-105, CE-104"],
  ["CE-107", "Build login and register screens", "minh.anh@teampulse.demo", "Evidence Linked", "High", excelDate("2026-10-12"), 7, "CE-102"],
  ["CE-108", "Create event list and filters", "minh.anh@teampulse.demo", "In Progress", "High", excelDate("2026-10-16"), 8, "CE-107"],
  ["CE-109", "Implement event CRUD API", "gia.huy@teampulse.demo", "In Progress", "High", excelDate("2026-10-16"), 8, "CE-106"],
  ["CE-110", "Create database migration and seed events", "thu.ha@teampulse.demo", "Done", "High", excelDate("2026-10-10"), 5, "CE-104"],
  ["CE-111", "Build event detail page", "minh.anh@teampulse.demo", "Todo", "Medium", excelDate("2026-10-20"), 6, "CE-108, CE-109"],
  ["CE-112", "Add event registration endpoint", "gia.huy@teampulse.demo", "Evidence Linked", "High", excelDate("2026-10-15"), 6, "CE-109"],
  ["CE-113", "Implement registration form and validation", "minh.anh@teampulse.demo", "Todo", "High", excelDate("2026-10-21"), 6, "CE-112, CE-111"],
  ["CE-114", "Write API integration tests", "thu.ha@teampulse.demo", "In Progress", "Medium", excelDate("2026-10-18"), 6, "CE-106, CE-109"],
  ["CE-115", "Prepare accessibility checklist", "thu.ha@teampulse.demo", "Todo", "Low", excelDate("2026-10-22"), 3, "CE-102"],
  ["CE-116", "Create admin event management page", "minh.anh@teampulse.demo", "Blocked", "Medium", excelDate("2026-10-19"), 7, "CE-106, CE-109"],
  ["CE-117", "Add role-based authorization policy", "gia.huy@teampulse.demo", "In Progress", "High", excelDate("2026-10-17"), 5, "CE-106"],
  ["CE-118", "Document API contracts", "thu.ha@teampulse.demo", "Todo", "Medium", excelDate("2026-10-23"), 4, "CE-109"],
  ["CE-119", "Create project demo slides", "bao.trung@teampulse.demo", "Todo", "Low", excelDate("2026-10-24"), 4, "CE-111, CE-113, CE-114"],
  ["CE-120", "Run final demo and retrospective", "bao.trung@teampulse.demo", "Todo", "Medium", excelDate("2026-10-25"), 3, "CE-114, CE-115, CE-116, CE-117, CE-118, CE-119"],
];

const workbook = Workbook.create();
const sheet = workbook.worksheets.add("Tasks");
sheet.showGridLines = false;

const headers = [["TaskCode", "Title", "AssigneeEmail", "Status", "Priority", "DueDate", "EstimateHours", "DependsOn"]];
sheet.getRange("A1:H1").values = headers;
sheet.getRange(`A2:H${tasks.length + 1}`).values = tasks;

sheet.getRange("A1:H1").format = {
  fill: "#303030",
  font: { name: "Aptos", size: 10, bold: true, color: "#FFFFFF" },
  horizontalAlignment: "center",
  verticalAlignment: "center",
  borders: { preset: "all", style: "thin", color: "#707070" },
};
sheet.getRange(`A2:H${tasks.length + 1}`).format = {
  font: { name: "Aptos", size: 10, color: "#1F1F1F" },
  verticalAlignment: "center",
  borders: { preset: "inside", style: "thin", color: "#E2E2E2" },
};
sheet.getRange(`F2:F${tasks.length + 1}`).format.numberFormat = "yyyy-mm-dd";
sheet.getRange(`G2:G${tasks.length + 1}`).format.numberFormat = "0.0";
sheet.getRange(`A2:A${tasks.length + 1}`).format.font = { name: "Aptos", size: 10, bold: true, color: "#8A6500" };

sheet.getRange(`D2:D${tasks.length + 1}`).dataValidation = {
  rule: { type: "list", values: ["Todo", "In Progress", "Evidence Linked", "In Review", "Needs Changes", "Blocked", "Done"] },
};
sheet.getRange(`E2:E${tasks.length + 1}`).dataValidation = {
  rule: { type: "list", values: ["Low", "Medium", "High"] },
};
sheet.getRange(`D2:D${tasks.length + 1}`).conditionalFormats.add("containsText", { text: "Blocked", format: { fill: "#FDE8E7", font: { color: "#A61B1B", bold: true } } });
sheet.getRange(`D2:D${tasks.length + 1}`).conditionalFormats.add("containsText", { text: "Done", format: { fill: "#E7F6EC", font: { color: "#1B6A38", bold: true } } });
sheet.getRange(`E2:E${tasks.length + 1}`).conditionalFormats.add("containsText", { text: "High", format: { fill: "#FFF1D6", font: { color: "#8A4B00", bold: true } } });

sheet.getRange("A:A").format.columnWidth = 13;
sheet.getRange("B:B").format.columnWidth = 40;
sheet.getRange("C:C").format.columnWidth = 30;
sheet.getRange("D:D").format.columnWidth = 18;
sheet.getRange("E:E").format.columnWidth = 12;
sheet.getRange("F:F").format.columnWidth = 14;
sheet.getRange("G:G").format.columnWidth = 16;
sheet.getRange("H:H").format.columnWidth = 32;
sheet.getRange("1:1").format.rowHeight = 22;
sheet.freezePanes.freezeRows(1);

sheet.tables.add(`A1:H${tasks.length + 1}`, true, "DemoTasks");
workbook.recalculate();
await fs.mkdir(path.dirname(outputPath), { recursive: true });
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(`Created ${outputPath}`);
