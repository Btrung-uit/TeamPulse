namespace TeamPulse.Application.Integrations;

public sealed record GitHubWebhookRequest(
    string? DeliveryId,
    string? EventName,
    string? Signature,
    byte[] Payload);
