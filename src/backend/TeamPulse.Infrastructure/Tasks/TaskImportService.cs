using System.Globalization;
using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TeamPulse.Application.Tasks;
using TeamPulse.Domain.Activities;
using TeamPulse.Domain.Imports;
using TeamPulse.Domain.Tasks;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Tasks;

internal sealed class TaskImportService(
    TeamPulseDbContext dbContext,
    TimeProvider timeProvider) : ITaskImportService
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const int MaximumPreviewRows = 2_000;

    public async Task<TaskImportResult<TaskImportUpload>> UploadAsync(
        Guid workspaceId,
        Guid projectId,
        Guid uploadedByUserId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.WorkspaceId == workspaceId && project.Id == projectId)
            .Select(project => new { project.IsArchived })
            .SingleOrDefaultAsync(cancellationToken);
        if (project is null)
        {
            return TaskImportResult<TaskImportUpload>.Failed(TaskImportFailure.ProjectNotFound);
        }

        if (project.IsArchived)
        {
            return TaskImportResult<TaskImportUpload>.Failed(TaskImportFailure.ProjectArchived);
        }

        var safeFileName = Path.GetFileName(fileName);
        if (!string.Equals(Path.GetExtension(safeFileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return TaskImportResult<TaskImportUpload>.Failed(TaskImportFailure.InvalidFileType);
        }

        if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName.Length > ImportBatch.FileNameMaximumLength)
        {
            return TaskImportResult<TaskImportUpload>.Failed(
                TaskImportFailure.InvalidWorkbook,
                $"The file name cannot exceed {ImportBatch.FileNameMaximumLength} characters.");
        }

        await using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > ITaskImportService.MaximumFileSizeBytes)
            {
                return TaskImportResult<TaskImportUpload>.Failed(TaskImportFailure.FileTooLarge);
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        if (buffer.Length == 0)
        {
            return TaskImportResult<TaskImportUpload>.Failed(
                TaskImportFailure.InvalidWorkbook,
                "The workbook is empty.");
        }

        var bytes = buffer.ToArray();
        IReadOnlyList<ImportSheet> sheets;
        try
        {
            using var workbook = new XLWorkbook(new MemoryStream(bytes));
            sheets = workbook.Worksheets
                .Select(ToSheet)
                .Where(sheet => sheet.Columns.Count > 0)
                .ToArray();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return TaskImportResult<TaskImportUpload>.Failed(
                TaskImportFailure.InvalidWorkbook,
                "The file is not a readable .xlsx workbook.");
        }

        if (sheets.Count == 0)
        {
            return TaskImportResult<TaskImportUpload>.Failed(TaskImportFailure.WorkbookEmpty);
        }

        var batch = new ImportBatch(
            Guid.NewGuid(),
            workspaceId,
            projectId,
            uploadedByUserId,
            safeFileName,
            ExcelContentType,
            bytes,
            Convert.ToHexString(SHA256.HashData(bytes)));
        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TaskImportResult<TaskImportUpload>.Success(new TaskImportUpload(
            batch.Id,
            batch.FileName,
            batch.FileSizeBytes,
            sheets));
    }

    public async Task<TaskImportResult<TaskImportPreview>> PreviewAsync(
        Guid workspaceId,
        Guid projectId,
        Guid batchId,
        TaskImportPreviewCommand command,
        CancellationToken cancellationToken = default)
    {
        var batch = await dbContext.ImportBatches
            .Include(item => item.Errors)
            .SingleOrDefaultAsync(
                item => item.Id == batchId && item.ProjectId == projectId,
                cancellationToken);
        if (batch is null)
        {
            return TaskImportResult<TaskImportPreview>.Failed(TaskImportFailure.BatchNotFound);
        }

        if (batch.Status == ImportBatchStatus.Confirmed)
        {
            return TaskImportResult<TaskImportPreview>.Failed(TaskImportFailure.BatchConfirmed);
        }

        using var workbook = new XLWorkbook(new MemoryStream(batch.Content));
        if (!workbook.Worksheets.TryGetWorksheet(command.SheetName, out var worksheet))
        {
            return TaskImportResult<TaskImportPreview>.Failed(TaskImportFailure.SheetNotFound);
        }

        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var mappingError = ValidateMapping(command.Mapping, lastColumn);
        if (mappingError is not null)
        {
            return TaskImportResult<TaskImportPreview>.Failed(
                TaskImportFailure.InvalidMapping,
                mappingError);
        }

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow - 1 > MaximumPreviewRows)
        {
            return TaskImportResult<TaskImportPreview>.Failed(
                TaskImportFailure.InvalidWorkbook,
                $"A sheet can contain at most {MaximumPreviewRows} data rows.");
        }

        var members = await dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(member => member.WorkspaceId == workspaceId && member.IsActive)
            .Include(member => member.User)
            .Select(member => member.User.Email)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);
        var storedTasks = await dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.WorkspaceId == workspaceId && task.ProjectId == projectId)
            .Select(task => new { task.Id, task.Code })
            .ToListAsync(cancellationToken);
        var existingCodes = storedTasks
            .Select(task => task.Code.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rows = Enumerable.Range(2, Math.Max(0, lastRow - 1))
            .Where(row => !IsMappedRowEmpty(worksheet.Row(row), command.Mapping))
            .Select(row => ReadRow(worksheet.Row(row), command.Mapping))
            .ToList();
        var codeCounts = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.TaskCode))
            .GroupBy(row => row.TaskCode!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var importCodes = codeCounts.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var previews = rows.Select(row => ValidateRow(
            row,
            members,
            existingCodes,
            codeCounts,
            importCodes)).ToArray();
        var existingLinks = await dbContext.TaskDependencies
            .AsNoTracking()
            .Where(link => link.WorkspaceId == workspaceId && link.ProjectId == projectId)
            .Select(link => new TaskDependencyLink(
                link.PredecessorTaskId,
                link.SuccessorTaskId))
            .ToListAsync(cancellationToken);
        var cycleErrors = ValidateDependencyGraph(
            rows,
            storedTasks.ToDictionary(
                task => task.Code.Value,
                task => task.Id,
                StringComparer.OrdinalIgnoreCase),
            existingLinks,
            codeCounts);
        previews = previews.Select(row => cycleErrors.TryGetValue(row.RowNumber, out var graphErrors)
            ? row with { Errors = row.Errors.Concat(graphErrors).ToArray() }
            : row).ToArray();
        var errors = previews.SelectMany(row => row.Errors).ToArray();

        dbContext.ImportErrorRows.RemoveRange(batch.Errors);
        dbContext.ImportErrorRows.AddRange(errors.Select(error => new ImportErrorRow(
            Guid.NewGuid(),
            batch.Id,
            error.RowNumber,
            error.Column,
            error.Reason,
            error.RawValue)));
        var errorRowCount = previews.Count(row => row.Errors.Count > 0);
        batch.RecordPreview(
            command.SheetName,
            JsonSerializer.Serialize(command.Mapping),
            previews.Length,
            previews.Length - errorRowCount,
            errorRowCount);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TaskImportResult<TaskImportPreview>.Success(new TaskImportPreview(
            batch.Id,
            batch.Status.ToString(),
            batch.TotalRows,
            batch.ValidRows,
            batch.ErrorRows,
            previews));
    }

    public async Task<TaskImportResult<TaskImportConfirmation>> ConfirmAsync(
        Guid workspaceId,
        Guid projectId,
        Guid batchId,
        Guid confirmedByUserId,
        CancellationToken cancellationToken = default)
    {
        IDbContextTransaction? transaction = null;
        try
        {
            if (dbContext.Database.IsRelational())
            {
                transaction = await dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
            }

            var project = await dbContext.Projects
                .AsNoTracking()
                .Where(item => item.WorkspaceId == workspaceId && item.Id == projectId)
                .Select(item => new { item.IsArchived })
                .SingleOrDefaultAsync(cancellationToken);
            if (project is null)
            {
                return TaskImportResult<TaskImportConfirmation>.Failed(
                    TaskImportFailure.ProjectNotFound);
            }

            if (project.IsArchived)
            {
                return TaskImportResult<TaskImportConfirmation>.Failed(
                    TaskImportFailure.ProjectArchived);
            }

            var batch = await dbContext.ImportBatches.SingleOrDefaultAsync(
                item => item.Id == batchId && item.ProjectId == projectId,
                cancellationToken);
            if (batch is null)
            {
                return TaskImportResult<TaskImportConfirmation>.Failed(
                    TaskImportFailure.BatchNotFound);
            }

            if (batch.Status == ImportBatchStatus.Confirmed)
            {
                return TaskImportResult<TaskImportConfirmation>.Success(new TaskImportConfirmation(
                    batch.Id,
                    batch.Status.ToString(),
                    batch.ImportedTaskCount,
                    batch.ConfirmedAtUtc!.Value,
                    true));
            }

            if (string.IsNullOrWhiteSpace(batch.SelectedSheet) ||
                string.IsNullOrWhiteSpace(batch.MappingJson))
            {
                return TaskImportResult<TaskImportConfirmation>.Failed(
                    TaskImportFailure.BatchNotValidated);
            }

            TaskImportMapping? mapping;
            try
            {
                mapping = JsonSerializer.Deserialize<TaskImportMapping>(batch.MappingJson);
            }
            catch (JsonException)
            {
                mapping = null;
            }

            if (mapping is null)
            {
                return TaskImportResult<TaskImportConfirmation>.Failed(
                    TaskImportFailure.BatchNotValidated);
            }

            var validation = await PreviewAsync(
                workspaceId,
                projectId,
                batchId,
                new TaskImportPreviewCommand(batch.SelectedSheet, mapping),
                cancellationToken);
            if (!validation.Succeeded)
            {
                return TaskImportResult<TaskImportConfirmation>.Failed(
                    validation.Failure,
                    validation.Detail);
            }

            if (validation.Value!.ErrorRows > 0)
            {
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return TaskImportResult<TaskImportConfirmation>.Failed(
                    TaskImportFailure.BatchHasErrors,
                    "The workbook changed or project data now conflicts with this import. Review the preview again.");
            }

            using var workbook = new XLWorkbook(new MemoryStream(batch.Content));
            var worksheet = workbook.Worksheet(batch.SelectedSheet);
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            var rawRows = Enumerable.Range(2, Math.Max(0, lastRow - 1))
                .Where(row => !IsMappedRowEmpty(worksheet.Row(row), mapping))
                .Select(row => ReadRow(worksheet.Row(row), mapping))
                .ToArray();

            var members = await dbContext.WorkspaceMembers
                .AsNoTracking()
                .Where(member => member.WorkspaceId == workspaceId && member.IsActive)
                .Include(member => member.User)
                .Select(member => new { member.UserId, member.User.Email })
                .ToListAsync(cancellationToken);
            var memberIds = members.ToDictionary(
                member => member.Email,
                member => member.UserId,
                StringComparer.OrdinalIgnoreCase);
            var existingTasks = await dbContext.Tasks
                .AsNoTracking()
                .Where(task => task.WorkspaceId == workspaceId && task.ProjectId == projectId)
                .Select(task => new { task.Id, task.Code })
                .ToListAsync(cancellationToken);
            var taskIdsByCode = existingTasks.ToDictionary(
                task => task.Code.Value,
                task => task.Id,
                StringComparer.OrdinalIgnoreCase);

            var imported = rawRows.Select(row => ToImportedTask(row, memberIds)).ToArray();
            foreach (var row in imported)
            {
                if (taskIdsByCode.ContainsKey(row.Code.Value))
                {
                    return TaskImportResult<TaskImportConfirmation>.Failed(
                        TaskImportFailure.ConfirmationConflict,
                        $"Task code '{row.Code.Value}' now exists. Refresh the preview before confirming.");
                }

                taskIdsByCode.Add(row.Code.Value, row.Id);
            }

            var links = await dbContext.TaskDependencies
                .AsNoTracking()
                .Where(link => link.WorkspaceId == workspaceId && link.ProjectId == projectId)
                .Select(link => new TaskDependencyLink(
                    link.PredecessorTaskId,
                    link.SuccessorTaskId))
                .ToListAsync(cancellationToken);
            var newDependencies = new List<TaskDependency>();
            foreach (var row in imported)
            {
                foreach (var dependencyCode in SplitDependencies(row.DependsOn))
                {
                    var predecessorId = taskIdsByCode[dependencyCode];
                    if (TaskDependencyGraph.WouldCreateCycle(links, predecessorId, row.Id))
                    {
                        return TaskImportResult<TaskImportConfirmation>.Failed(
                            TaskImportFailure.BatchHasErrors,
                            $"Dependency '{dependencyCode}' creates a cycle for '{row.Code.Value}'.");
                    }

                    links.Add(new TaskDependencyLink(predecessorId, row.Id));
                    newDependencies.Add(new TaskDependency(
                        workspaceId,
                        projectId,
                        predecessorId,
                        row.Id));
                }
            }

            var now = timeProvider.GetUtcNow();
            foreach (var row in imported)
            {
                var task = new ProjectTask(
                    row.Id,
                    workspaceId,
                    projectId,
                    confirmedByUserId,
                    row.Code,
                    row.Title,
                    priority: row.Priority);
                task.AssignTo(row.AssigneeUserId);
                task.Schedule(row.DueOn, row.EstimateHours);
                task.InitializeImportedStatus(row.Status);
                dbContext.Tasks.Add(task);
                dbContext.ActivityLogs.Add(new ActivityLog(
                    Guid.NewGuid(),
                    workspaceId,
                    confirmedByUserId,
                    ActivityAction.TaskCreated,
                    nameof(ProjectTask),
                    task.Id,
                    now,
                    newValue: task.Code.Value));
            }

            dbContext.TaskDependencies.AddRange(newDependencies);
            batch.MarkConfirmed(confirmedByUserId, now, imported.Length);
            dbContext.ActivityLogs.Add(new ActivityLog(
                Guid.NewGuid(),
                workspaceId,
                confirmedByUserId,
                ActivityAction.ImportConfirmed,
                nameof(ImportBatch),
                batch.Id,
                now,
                newValue: imported.Length.ToString(CultureInfo.InvariantCulture)));

            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return TaskImportResult<TaskImportConfirmation>.Success(new TaskImportConfirmation(
                batch.Id,
                batch.Status.ToString(),
                batch.ImportedTaskCount,
                batch.ConfirmedAtUtc!.Value,
                false));
        }
        catch (DbUpdateException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            return TaskImportResult<TaskImportConfirmation>.Failed(
                TaskImportFailure.ConfirmationConflict,
                "The import was changed or confirmed by another request. Refresh import history.");
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<TaskImportResult<IReadOnlyList<TaskImportHistoryItem>>> ListHistoryAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Projects.AsNoTracking().AnyAsync(
            project => project.WorkspaceId == workspaceId && project.Id == projectId,
            cancellationToken))
        {
            return TaskImportResult<IReadOnlyList<TaskImportHistoryItem>>.Failed(
                TaskImportFailure.ProjectNotFound);
        }

        var history = await dbContext.ImportBatches
            .AsNoTracking()
            .Where(batch => batch.ProjectId == projectId)
            .OrderByDescending(batch => batch.CreatedAtUtc)
            .Take(20)
            .Select(batch => new TaskImportHistoryItem(
                batch.Id,
                batch.FileName,
                batch.Status.ToString(),
                batch.TotalRows,
                batch.ValidRows,
                batch.ErrorRows,
                batch.ImportedTaskCount,
                batch.CreatedAtUtc,
                batch.ConfirmedAtUtc))
            .ToListAsync(cancellationToken);
        return TaskImportResult<IReadOnlyList<TaskImportHistoryItem>>.Success(history);
    }

    private static ImportedTask ToImportedTask(
        RawRow row,
        IReadOnlyDictionary<string, Guid> memberIds)
    {
        DateOnly? dueOn = null;
        decimal? estimateHours = null;
        if (!string.IsNullOrWhiteSpace(row.DueDate))
        {
            TryDate(row.DueDateCell!, out var parsedDueOn);
            dueOn = parsedDueOn;
        }

        if (!string.IsNullOrWhiteSpace(row.EstimateHours))
        {
            TryDecimal(row.EstimateHoursCell!, out var parsedEstimateHours);
            estimateHours = parsedEstimateHours;
        }

        return new ImportedTask(
            Guid.NewGuid(),
            TaskCode.Parse(row.TaskCode),
            row.Title!,
            string.IsNullOrWhiteSpace(row.AssigneeEmail) ? null : memberIds[row.AssigneeEmail],
            Enum.Parse<ProjectTaskStatus>(NormalizeEnum(row.Status), true),
            Enum.Parse<TaskPriority>(NormalizeEnum(row.Priority), true),
            dueOn,
            estimateHours,
            row.DependsOn);
    }

    private static ImportSheet ToSheet(IXLWorksheet worksheet)
    {
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        return new ImportSheet(
            worksheet.Name,
            Math.Max(0, lastRow - 1),
            Enumerable.Range(1, lastColumn)
                .Select(number => new ImportColumn(
                    number,
                    string.IsNullOrWhiteSpace(worksheet.Cell(1, number).GetString())
                        ? $"Column {number}"
                        : worksheet.Cell(1, number).GetString().Trim()))
                .ToArray());
    }

    private static string? ValidateMapping(TaskImportMapping mapping, int lastColumn)
    {
        var required = new[] { mapping.TaskCode, mapping.Title, mapping.Status, mapping.Priority };
        var all = new int?[]
        {
            mapping.TaskCode, mapping.Title, mapping.AssigneeEmail, mapping.Status,
            mapping.Priority, mapping.DueDate, mapping.EstimateHours, mapping.DependsOn,
        };
        if (required.Any(column => column < 1 || column > lastColumn) ||
            all.Where(column => column.HasValue).Any(column => column < 1 || column > lastColumn))
        {
            return "Every mapped column must exist in the selected sheet.";
        }

        if (all.Where(column => column.HasValue).Select(column => column!.Value).Distinct().Count() !=
            all.Count(column => column.HasValue))
        {
            return "A source column cannot be mapped to more than one TeamPulse field.";
        }

        return null;
    }

    private static bool IsMappedRowEmpty(IXLRow row, TaskImportMapping mapping)
    {
        var columns = new int?[]
        {
            mapping.TaskCode, mapping.Title, mapping.AssigneeEmail, mapping.Status,
            mapping.Priority, mapping.DueDate, mapping.EstimateHours, mapping.DependsOn,
        };
        return columns.Where(column => column.HasValue)
            .All(column => row.Cell(column!.Value).IsEmpty());
    }

    private static RawRow ReadRow(IXLRow row, TaskImportMapping mapping) => new(
        row.RowNumber(),
        Text(row, mapping.TaskCode),
        Text(row, mapping.Title),
        Text(row, mapping.AssigneeEmail),
        Text(row, mapping.Status),
        Text(row, mapping.Priority),
        Text(row, mapping.DueDate),
        Text(row, mapping.EstimateHours),
        Text(row, mapping.DependsOn),
        mapping.DueDate.HasValue ? row.Cell(mapping.DueDate.Value) : null,
        mapping.EstimateHours.HasValue ? row.Cell(mapping.EstimateHours.Value) : null);

    private static string? Text(IXLRow row, int? column) =>
        column.HasValue ? row.Cell(column.Value).GetFormattedString().Trim() : null;

    private static TaskImportPreviewRow ValidateRow(
        RawRow row,
        HashSet<string> members,
        HashSet<string> existingCodes,
        IReadOnlyDictionary<string, int> codeCounts,
        HashSet<string> importCodes)
    {
        var errors = new List<TaskImportCellError>();
        var code = row.TaskCode?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            Add("TaskCode", "TaskCode is required.", row.TaskCode);
        }
        else
        {
            try
            {
                code = TaskCode.Parse(code).Value;
            }
            catch (ArgumentException)
            {
                Add("TaskCode", "TaskCode must use uppercase letters/numbers and one hyphen, for example TP-041.", row.TaskCode);
            }

            if (existingCodes.Contains(code)) Add("TaskCode", "TaskCode already exists in this project.", row.TaskCode);
            if (codeCounts.GetValueOrDefault(code) > 1) Add("TaskCode", "TaskCode is duplicated in this workbook.", row.TaskCode);
        }

        if (string.IsNullOrWhiteSpace(row.Title)) Add("Title", "Title is required.", row.Title);
        else if (row.Title.Length > ProjectTask.TitleMaximumLength) Add("Title", $"Title cannot exceed {ProjectTask.TitleMaximumLength} characters.", row.Title);

        if (!string.IsNullOrWhiteSpace(row.AssigneeEmail) && !members.Contains(row.AssigneeEmail))
            Add("AssigneeEmail", "AssigneeEmail must belong to an active workspace member.", row.AssigneeEmail);

        if (!Enum.TryParse<ProjectTaskStatus>(NormalizeEnum(row.Status), true, out var status) || !Enum.IsDefined(status))
            Add("Status", "Status is required and must be a supported task status.", row.Status);
        if (!Enum.TryParse<TaskPriority>(NormalizeEnum(row.Priority), true, out var priority) || !Enum.IsDefined(priority))
            Add("Priority", "Priority is required and must be Low, Medium, High, or Urgent.", row.Priority);

        if (!string.IsNullOrWhiteSpace(row.DueDate) && !TryDate(row.DueDateCell!, out _))
            Add("DueDate", "DueDate must be a valid Excel date or ISO date (yyyy-MM-dd).", row.DueDate);
        if (!string.IsNullOrWhiteSpace(row.EstimateHours) &&
            (!TryDecimal(row.EstimateHoursCell!, out var estimate) || estimate <= 0 || estimate > ProjectTask.EstimateHoursMaximum))
            Add("EstimateHours", $"EstimateHours must be greater than 0 and no more than {ProjectTask.EstimateHoursMaximum}.", row.EstimateHours);

        foreach (var dependency in SplitDependencies(row.DependsOn))
        {
            if (string.Equals(dependency, code, StringComparison.OrdinalIgnoreCase))
                Add("DependsOn", "A task cannot depend on itself.", dependency);
            else if (!existingCodes.Contains(dependency) && !importCodes.Contains(dependency))
                Add("DependsOn", $"Dependency '{dependency}' was not found in this project or workbook.", dependency);
        }

        return new TaskImportPreviewRow(
            row.RowNumber, code, row.Title, row.AssigneeEmail, row.Status, row.Priority,
            row.DueDate, row.EstimateHours, row.DependsOn, errors);

        void Add(string column, string reason, string? value) =>
            errors.Add(new TaskImportCellError(row.RowNumber, column, reason, value));
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<TaskImportCellError>> ValidateDependencyGraph(
        IReadOnlyList<RawRow> rows,
        Dictionary<string, Guid> taskIdsByCode,
        List<TaskDependencyLink> links,
        IReadOnlyDictionary<string, int> codeCounts)
    {
        var errors = new Dictionary<int, IReadOnlyList<TaskImportCellError>>();
        foreach (var row in rows)
        {
            try
            {
                var code = TaskCode.Parse(row.TaskCode).Value;
                if (codeCounts.GetValueOrDefault(code) == 1 && !taskIdsByCode.ContainsKey(code))
                {
                    taskIdsByCode.Add(code, Guid.NewGuid());
                }
            }
            catch (ArgumentException)
            {
                // The regular row validator reports the invalid code.
            }
        }

        foreach (var row in rows)
        {
            string code;
            try
            {
                code = TaskCode.Parse(row.TaskCode).Value;
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (codeCounts.GetValueOrDefault(code) != 1 || !taskIdsByCode.TryGetValue(code, out var successorId))
            {
                continue;
            }

            var rowErrors = new List<TaskImportCellError>();
            var seenDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dependencyCode in SplitDependencies(row.DependsOn))
            {
                if (!seenDependencies.Add(dependencyCode))
                {
                    rowErrors.Add(new TaskImportCellError(
                        row.RowNumber,
                        "DependsOn",
                        $"Dependency '{dependencyCode}' is listed more than once.",
                        dependencyCode));
                    continue;
                }

                if (!taskIdsByCode.TryGetValue(dependencyCode, out var predecessorId) ||
                    predecessorId == successorId)
                {
                    continue;
                }

                if (TaskDependencyGraph.WouldCreateCycle(links, predecessorId, successorId))
                {
                    rowErrors.Add(new TaskImportCellError(
                        row.RowNumber,
                        "DependsOn",
                        $"Dependency '{dependencyCode}' creates a cycle.",
                        dependencyCode));
                    continue;
                }

                links.Add(new TaskDependencyLink(predecessorId, successorId));
            }

            if (rowErrors.Count > 0)
            {
                errors[row.RowNumber] = rowErrors;
            }
        }

        return errors;
    }

    private static string NormalizeEnum(string? value) =>
        (value ?? string.Empty)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);

    private static IEnumerable<string> SplitDependencies(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => item.ToUpperInvariant());

    private static bool TryDate(IXLCell cell, out DateOnly value)
    {
        if (cell.TryGetValue<DateTime>(out var date))
        {
            value = DateOnly.FromDateTime(date);
            return true;
        }

        return DateOnly.TryParseExact(
            cell.GetString().Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out value);
    }

    private static bool TryDecimal(IXLCell cell, out decimal value)
    {
        if (cell.TryGetValue<decimal>(out value)) return true;
        return decimal.TryParse(cell.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private sealed record RawRow(
        int RowNumber,
        string? TaskCode,
        string? Title,
        string? AssigneeEmail,
        string? Status,
        string? Priority,
        string? DueDate,
        string? EstimateHours,
        string? DependsOn,
        IXLCell? DueDateCell,
        IXLCell? EstimateHoursCell);

    private sealed record ImportedTask(
        Guid Id,
        TaskCode Code,
        string Title,
        Guid? AssigneeUserId,
        ProjectTaskStatus Status,
        TaskPriority Priority,
        DateOnly? DueOn,
        decimal? EstimateHours,
        string? DependsOn);
}
