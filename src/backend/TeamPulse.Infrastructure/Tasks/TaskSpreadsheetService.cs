using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using TeamPulse.Application.Tasks;
using TeamPulse.Domain.Tasks;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Tasks;

internal sealed class TaskSpreadsheetService(
    TeamPulseDbContext dbContext,
    TimeProvider timeProvider) : ITaskSpreadsheetService
{
    private const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const int LastTemplateRow = 1_000;
    private static readonly string[] Headers =
    [
        "TaskCode",
        "Title",
        "AssigneeEmail",
        "Status",
        "Priority",
        "DueDate",
        "EstimateHours",
        "DependsOn",
    ];

    public async Task<TaskResult<TaskSpreadsheetFile>> CreateTemplateAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.Id == projectId)
            .Select(item => new { item.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (project is null)
        {
            return TaskResult<TaskSpreadsheetFile>.Failed(TaskFailure.ProjectNotFound);
        }

        using var workbook = CreateWorkbook(
            $"{project.Name} task import template",
            "Template for importing project tasks into TeamPulse.");
        var tasks = workbook.Worksheets.Add("Tasks");
        WriteHeaders(tasks);
        ApplyTaskSheetLayout(tasks, LastTemplateRow);
        AddTemplateValidation(tasks);

        var example = workbook.Worksheets.Add("Example - do not import");
        WriteHeaders(example);
        WriteRow(
            example.Row(2),
            new SpreadsheetRow(
                "TP-001",
                "Build login page",
                "member@example.com",
                "Todo",
                "High",
                new DateOnly(2026, 11, 15),
                8m,
                string.Empty));
        WriteRow(
            example.Row(3),
            new SpreadsheetRow(
                "TP-002",
                "Connect authentication API",
                "member@example.com",
                "Todo",
                "Medium",
                new DateOnly(2026, 11, 18),
                5.5m,
                "TP-001"));
        ApplyTaskSheetLayout(example, 3);

        return TaskResult<TaskSpreadsheetFile>.Success(new TaskSpreadsheetFile(
            $"{FileSlug(project.Name)}-task-template.xlsx",
            ContentType,
            Save(workbook)));
    }

    public async Task<TaskResult<TaskSpreadsheetFile>> ExportAsync(
        Guid workspaceId,
        Guid projectId,
        TaskSpreadsheetQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.Id == projectId)
            .Select(item => new { item.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (project is null)
        {
            return TaskResult<TaskSpreadsheetFile>.Failed(TaskFailure.ProjectNotFound);
        }

        var tasks = dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.WorkspaceId == workspaceId && task.ProjectId == projectId);

        if (!query.IncludeArchived)
        {
            tasks = tasks.Where(task => !task.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            if (TryParseTaskCode(search, out var code))
            {
                tasks = tasks.Where(task => task.Code == code);
            }
            else
            {
                tasks = tasks.Where(task => task.Title.Contains(search));
            }
        }

        if (query.Status.HasValue)
        {
            tasks = tasks.Where(task => task.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            tasks = tasks.Where(task => task.Priority == query.Priority.Value);
        }

        if (query.AssigneeUserId.HasValue)
        {
            tasks = tasks.Where(task => task.AssigneeUserId == query.AssigneeUserId.Value);
        }

        if (query.SprintId.HasValue)
        {
            tasks = tasks.Where(task => task.SprintId == query.SprintId.Value);
        }

        if (query.DueBefore.HasValue)
        {
            tasks = tasks.Where(task =>
                task.DueOn.HasValue && task.DueOn.Value <= query.DueBefore.Value);
        }

        if (query.OpenOnly)
        {
            tasks = tasks.Where(task => task.Status != ProjectTaskStatus.Done);
        }

        if (query.AttentionOnly)
        {
            var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            var dueSoon = today.AddDays(7);
            tasks = tasks.Where(task =>
                task.Status == ProjectTaskStatus.Blocked ||
                (task.Status != ProjectTaskStatus.Done &&
                    task.DueOn.HasValue &&
                    task.DueOn.Value >= today &&
                    task.DueOn.Value <= dueSoon));
        }

        var entities = await tasks
            .Include(task => task.AssigneeMember)
                .ThenInclude(member => member!.User)
            .Include(task => task.PredecessorDependencies)
                .ThenInclude(dependency => dependency.PredecessorTask)
            .ToListAsync(cancellationToken);
        var rows = ApplySort(entities, query.Sort)
            .Select(task => new SpreadsheetRow(
                task.Code.Value,
                task.Title,
                task.AssigneeMember == null ? string.Empty : task.AssigneeMember.User.Email,
                task.Status.ToString(),
                task.Priority.ToString(),
                task.DueOn,
                task.EstimateHours,
                string.Join(
                    ", ",
                    task.PredecessorDependencies
                        .OrderBy(
                            dependency => dependency.PredecessorTask.Code.Value,
                            StringComparer.Ordinal)
                        .Select(dependency => dependency.PredecessorTask.Code.Value))))
            .ToArray();

        using var workbook = CreateWorkbook(
            $"{project.Name} task snapshot",
            "Filtered TeamPulse task snapshot.");
        var worksheet = workbook.Worksheets.Add("Tasks");
        WriteHeaders(worksheet);
        for (var index = 0; index < rows.Length; index++)
        {
            WriteRow(worksheet.Row(index + 2), rows[index]);
        }

        ApplyTaskSheetLayout(worksheet, Math.Max(rows.Length + 1, 2));
        if (rows.Length > 0)
        {
            var table = worksheet.Range(1, 1, rows.Length + 1, Headers.Length)
                .CreateTable("TasksTable");
            table.Theme = XLTableTheme.TableStyleMedium2;
        }
        else
        {
            worksheet.Range(1, 1, 1, Headers.Length).SetAutoFilter();
        }

        var snapshotDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return TaskResult<TaskSpreadsheetFile>.Success(new TaskSpreadsheetFile(
            $"{FileSlug(project.Name)}-tasks-{snapshotDate:yyyy-MM-dd}.xlsx",
            ContentType,
            Save(workbook)));
    }

    private static XLWorkbook CreateWorkbook(string title, string subject)
    {
        var workbook = new XLWorkbook();
        workbook.Properties.Author = "TeamPulse";
        workbook.Properties.Title = title;
        workbook.Properties.Subject = subject;
        return workbook;
    }

    private static void WriteHeaders(IXLWorksheet worksheet)
    {
        for (var index = 0; index < Headers.Length; index++)
        {
            worksheet.Cell(1, index + 1).SetValue(Headers[index]);
        }

        var header = worksheet.Range(1, 1, 1, Headers.Length);
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#171717");
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Font.Bold = true;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Row(1).Height = 24;
    }

    private static void WriteRow(IXLRow row, SpreadsheetRow value)
    {
        row.Cell(1).SetValue(value.TaskCode);
        row.Cell(2).SetValue(value.Title);
        row.Cell(3).SetValue(value.AssigneeEmail);
        row.Cell(4).SetValue(value.Status);
        row.Cell(5).SetValue(value.Priority);
        if (value.DueDate.HasValue)
        {
            row.Cell(6).SetValue(value.DueDate.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (value.EstimateHours.HasValue)
        {
            row.Cell(7).SetValue(value.EstimateHours.Value);
        }

        row.Cell(8).SetValue(value.DependsOn);
    }

    private static void ApplyTaskSheetLayout(IXLWorksheet worksheet, int lastRow)
    {
        worksheet.ShowGridLines = false;
        worksheet.SheetView.FreezeRows(1);
        worksheet.Column(1).Width = 15;
        worksheet.Column(2).Width = 34;
        worksheet.Column(3).Width = 30;
        worksheet.Column(4).Width = 18;
        worksheet.Column(5).Width = 14;
        worksheet.Column(6).Width = 14;
        worksheet.Column(7).Width = 16;
        worksheet.Column(8).Width = 22;
        worksheet.Range(2, 6, lastRow, 6).Style.DateFormat.Format = "yyyy-mm-dd";
        worksheet.Range(2, 7, lastRow, 7).Style.NumberFormat.Format = "0.00";
        worksheet.Range(2, 1, lastRow, Headers.Length).Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;
        worksheet.Range(2, 1, lastRow, Headers.Length).Style.Alignment.WrapText = false;
    }

    private static void AddTemplateValidation(IXLWorksheet worksheet)
    {
        worksheet.Range($"D2:D{LastTemplateRow}")
            .CreateDataValidation()
            .List("Todo,InProgress,EvidenceLinked,InReview,NeedsChanges,Blocked,Done");
        worksheet.Range($"E2:E{LastTemplateRow}")
            .CreateDataValidation()
            .List("Low,Medium,High,Urgent");
        worksheet.Range($"G2:G{LastTemplateRow}")
            .CreateDataValidation()
            .Decimal.Between(0.01, 9_999.99);
    }

    private static IOrderedEnumerable<ProjectTask> ApplySort(
        IEnumerable<ProjectTask> tasks,
        TaskExportSort sort) => sort switch
        {
            TaskExportSort.UpdatedDescending => tasks
                .OrderByDescending(task => task.UpdatedAtUtc)
                .ThenBy(task => task.Code.Value, StringComparer.Ordinal),
            TaskExportSort.CodeAscending => tasks
                .OrderBy(task => task.Code.Value, StringComparer.Ordinal),
            TaskExportSort.PriorityDescending => tasks
                .OrderByDescending(task => task.Priority)
                .ThenBy(task => task.DueOn == null)
                .ThenBy(task => task.DueOn)
                .ThenBy(task => task.Code.Value, StringComparer.Ordinal),
            _ => tasks
                .OrderBy(task => task.DueOn == null)
                .ThenBy(task => task.DueOn)
                .ThenBy(task => task.Code.Value, StringComparer.Ordinal),
        };

    private static bool TryParseTaskCode(string value, out TaskCode code)
    {
        try
        {
            code = TaskCode.Parse(value);
            return true;
        }
        catch (ArgumentException)
        {
            code = default;
            return false;
        }
    }

    private static string FileSlug(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var result = builder.ToString().Trim('-');
        return string.IsNullOrEmpty(result) ? "project" : result[..Math.Min(result.Length, 60)];
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed record SpreadsheetRow(
        string TaskCode,
        string Title,
        string AssigneeEmail,
        string Status,
        string Priority,
        DateOnly? DueDate,
        decimal? EstimateHours,
        string DependsOn);
}
