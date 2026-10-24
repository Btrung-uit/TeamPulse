using Microsoft.Extensions.Options;
using TeamPulse.Application.Integrations;
using TeamPulse.Infrastructure.Integrations.GitHub;

namespace TeamPulse.Api.Endpoints;

public static class GitHubWebhookEndpoints
{
    private const string DeliveryHeader = "X-GitHub-Delivery";
    private const string EventHeader = "X-GitHub-Event";
    private const string SignatureHeader = "X-Hub-Signature-256";

    public static IEndpointRouteBuilder MapGitHubWebhookEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/integrations/github/webhook", ReceiveAsync)
            .WithTags("Integrations")
            .AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> ReceiveAsync(
        HttpRequest request,
        IGitHubWebhookReceiver receiver,
        IOptions<GitHubWebhookOptions> options,
        CancellationToken cancellationToken)
    {
        var maximumPayloadBytes = options.Value.MaximumPayloadBytes;
        if (!options.Value.Enabled)
        {
            return Results.Json(
                new
                {
                    code = "integration_disabled",
                    message = "GitHub webhook integration is disabled.",
                },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (request.ContentLength > maximumPayloadBytes)
        {
            return Results.Json(
                new { code = "payload_too_large", message = "Webhook payload is too large." },
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var payload = await ReadPayloadAsync(request.Body, maximumPayloadBytes, cancellationToken);
        if (payload is null)
        {
            return Results.Json(
                new { code = "payload_too_large", message = "Webhook payload is too large." },
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var result = await receiver.ReceiveAsync(
            new GitHubWebhookRequest(
                request.Headers[DeliveryHeader].ToString(),
                request.Headers[EventHeader].ToString(),
                request.Headers[SignatureHeader].ToString(),
                payload),
            cancellationToken);

        var response = new
        {
            outcome = result.Outcome.ToString(),
            result.Message,
        };
        return result.Outcome switch
        {
            GitHubWebhookReceiveOutcome.Accepted or
                GitHubWebhookReceiveOutcome.Duplicate or
                GitHubWebhookReceiveOutcome.Ignored => Results.Accepted(value: response),
            GitHubWebhookReceiveOutcome.InvalidSignature => Results.Json(
                new { code = "invalid_signature", result.Message },
                statusCode: StatusCodes.Status401Unauthorized),
            GitHubWebhookReceiveOutcome.InvalidRequest => Results.BadRequest(
                new { code = "invalid_webhook", result.Message }),
            GitHubWebhookReceiveOutcome.PayloadTooLarge => Results.Json(
                new { code = "payload_too_large", result.Message },
                statusCode: StatusCodes.Status413PayloadTooLarge),
            GitHubWebhookReceiveOutcome.Disabled => Results.Json(
                new { code = "integration_disabled", result.Message },
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => throw new InvalidOperationException("Unknown GitHub webhook result."),
        };
    }

    private static async Task<byte[]?> ReadPayloadAsync(
        Stream body,
        int maximumPayloadBytes,
        CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await body.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maximumPayloadBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
    }
}
