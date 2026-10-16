namespace TeamPulse.Api.Contracts;

public sealed record ApiErrorResponse(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Errors = null);
