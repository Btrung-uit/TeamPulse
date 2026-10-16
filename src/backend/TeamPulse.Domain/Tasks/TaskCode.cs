using System.Text.RegularExpressions;

namespace TeamPulse.Domain.Tasks;

public readonly partial record struct TaskCode
{
    public const int MaximumLength = 17;

    private TaskCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static TaskCode Parse(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Task code is required.", nameof(value));
        }

        if (!TaskCodePattern().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Task code must use PREFIX-NUMBER format, for example TP-123.",
                nameof(value));
        }

        return new TaskCode(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,9}-[0-9]{1,6}$", RegexOptions.CultureInvariant)]
    private static partial Regex TaskCodePattern();
}
