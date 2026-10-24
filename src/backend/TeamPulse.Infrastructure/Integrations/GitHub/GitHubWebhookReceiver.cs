using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TeamPulse.Application.Integrations;
using TeamPulse.Domain.Integrations;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Integrations.GitHub;

internal sealed class GitHubWebhookReceiver(
    TeamPulseDbContext dbContext,
    IOptions<GitHubWebhookOptions> options) : IGitHubWebhookReceiver
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>?> SupportedEvents =
        new Dictionary<string, HashSet<string>?>(StringComparer.Ordinal)
        {
            ["push"] = null,
            ["pull_request"] = new(StringComparer.Ordinal)
            {
                "opened",
                "reopened",
                "synchronize",
                "ready_for_review",
                "converted_to_draft",
                "closed",
            },
            ["pull_request_review"] = new(StringComparer.Ordinal)
            {
                "submitted",
                "edited",
                "dismissed",
            },
        };

    private readonly GitHubWebhookOptions _options = options.Value;

    public async Task<GitHubWebhookReceiveResult> ReceiveAsync(
        GitHubWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return Result(GitHubWebhookReceiveOutcome.Disabled, "GitHub webhook integration is disabled.");
        }

        if (request.Payload.Length > _options.MaximumPayloadBytes)
        {
            return Result(GitHubWebhookReceiveOutcome.PayloadTooLarge, "Webhook payload is too large.");
        }

        if (!IsValidHeader(request.DeliveryId, GitHubWebhookDelivery.DeliveryIdMaximumLength) ||
            !IsValidHeader(request.EventName, GitHubWebhookDelivery.EventNameMaximumLength))
        {
            return Result(
                GitHubWebhookReceiveOutcome.InvalidRequest,
                "GitHub delivery and event headers are required.");
        }

        if (!HasValidSignature(request.Payload, request.Signature))
        {
            return Result(GitHubWebhookReceiveOutcome.InvalidSignature, "Webhook signature is invalid.");
        }

        ParsedPayload parsedPayload;
        try
        {
            parsedPayload = ParsePayload(request.Payload);
        }
        catch (JsonException)
        {
            return Result(GitHubWebhookReceiveOutcome.InvalidRequest, "Webhook payload must be valid JSON.");
        }

        if (parsedPayload.Action?.Length > GitHubWebhookDelivery.ActionMaximumLength ||
            parsedPayload.RepositoryFullName?.Length > GitHubWebhookDelivery.RepositoryNameMaximumLength)
        {
            return Result(
                GitHubWebhookReceiveOutcome.InvalidRequest,
                "Webhook action or repository metadata is invalid.");
        }

        var deliveryId = request.DeliveryId!;
        var eventName = request.EventName!;
        if (await dbContext.GitHubWebhookDeliveries
                .AnyAsync(delivery => delivery.DeliveryId == deliveryId, cancellationToken))
        {
            return Result(GitHubWebhookReceiveOutcome.Duplicate, "Webhook delivery was already accepted.");
        }

        var ignoredReason = GetIgnoredReason(eventName, parsedPayload.Action);
        var delivery = new GitHubWebhookDelivery(
            Guid.NewGuid(),
            deliveryId,
            eventName,
            parsedPayload.Action,
            parsedPayload.RepositoryId,
            parsedPayload.RepositoryFullName,
            Encoding.UTF8.GetString(request.Payload),
            Convert.ToHexString(SHA256.HashData(request.Payload)).ToLowerInvariant(),
            ignoredReason is null,
            ignoredReason);
        dbContext.GitHubWebhookDeliveries.Add(delivery);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateDelivery(exception))
        {
            return Result(GitHubWebhookReceiveOutcome.Duplicate, "Webhook delivery was already accepted.");
        }

        return ignoredReason is null
            ? Result(GitHubWebhookReceiveOutcome.Accepted, "Webhook delivery was accepted.")
            : Result(GitHubWebhookReceiveOutcome.Ignored, ignoredReason);
    }

    private bool HasValidSignature(byte[] payload, string? providedSignature)
    {
        const string prefix = "sha256=";
        if (string.IsNullOrWhiteSpace(providedSignature) ||
            !providedSignature.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        byte[] providedHash;
        try
        {
            providedHash = Convert.FromHexString(providedSignature[prefix.Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        var secret = Encoding.UTF8.GetBytes(_options.Secret);
        var expectedHash = HMACSHA256.HashData(secret, payload);
        return providedHash.Length == expectedHash.Length &&
            CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }

    private static bool IsValidHeader(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximumLength &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    private static ParsedPayload ParsePayload(byte[] payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("GitHub webhook payload must be a JSON object.");
        }

        var action = root.TryGetProperty("action", out var actionElement) &&
            actionElement.ValueKind == JsonValueKind.String
                ? actionElement.GetString()
                : null;

        long? repositoryId = null;
        string? repositoryFullName = null;
        if (root.TryGetProperty("repository", out var repository) &&
            repository.ValueKind == JsonValueKind.Object)
        {
            if (repository.TryGetProperty("id", out var idElement) && idElement.TryGetInt64(out var id))
            {
                repositoryId = id;
            }

            if (repository.TryGetProperty("full_name", out var nameElement) &&
                nameElement.ValueKind == JsonValueKind.String)
            {
                repositoryFullName = nameElement.GetString();
            }
        }

        return new ParsedPayload(action, repositoryId, repositoryFullName);
    }

    private static string? GetIgnoredReason(string eventName, string? action)
    {
        if (string.Equals(eventName, "ping", StringComparison.Ordinal))
        {
            return "GitHub ping acknowledged; no background processing is required.";
        }

        if (!SupportedEvents.TryGetValue(eventName, out var supportedActions))
        {
            return $"GitHub event '{eventName}' is not supported.";
        }

        if (supportedActions is not null &&
            (string.IsNullOrWhiteSpace(action) || !supportedActions.Contains(action)))
        {
            return $"GitHub action '{action ?? "(missing)"}' is not supported for event '{eventName}'.";
        }

        return null;
    }

    private static bool IsDuplicateDelivery(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static GitHubWebhookReceiveResult Result(
        GitHubWebhookReceiveOutcome outcome,
        string message) => new(outcome, message);

    private sealed record ParsedPayload(
        string? Action,
        long? RepositoryId,
        string? RepositoryFullName);
}
