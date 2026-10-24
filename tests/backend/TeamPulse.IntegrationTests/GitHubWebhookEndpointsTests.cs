using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeamPulse.Domain.Integrations;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.IntegrationTests;

public sealed class GitHubWebhookEndpointsTests : IClassFixture<TeamPulseApiFactory>
{
    private const string WebhookPath = "/api/integrations/github/webhook";
    private const string WebhookSecret = "TeamPulse_GitHub_webhook_test_secret_2026";
    private readonly TeamPulseApiFactory _factory;

    public GitHubWebhookEndpointsTests(TeamPulseApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Webhook_RejectsPayloadWithInvalidSignature()
    {
        await using var factory = CreateEnabledFactory();
        using var client = CreateClient(factory);
        var payload = CreatePayload("ready_for_review");
        using var request = CreateRequest("delivery-invalid-signature", payload, "sha256=00");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_signature", body.GetProperty("code").GetString());
        Assert.Equal(0, await CountDeliveriesAsync(factory, "delivery-invalid-signature"));
    }

    [Fact]
    public async Task Webhook_AcceptsValidPayloadAndProcessesItAsynchronously()
    {
        await using var factory = CreateEnabledFactory();
        using var client = CreateClient(factory);
        var payload = CreatePayload("ready_for_review");
        using var request = CreateRequest(
            "delivery-valid",
            payload,
            CreateSignature(payload));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var delivery = await WaitForDeliveryAsync(factory, "delivery-valid");
        Assert.Equal(GitHubWebhookDeliveryStatus.Processed, delivery.Status);
        Assert.Equal("pull_request", delivery.EventName);
        Assert.Equal("ready_for_review", delivery.Action);
        Assert.Equal(123456789L, delivery.RepositoryId);
        Assert.Equal("Btrung-uit/TeamPulse", delivery.RepositoryFullName);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.NotNull(delivery.ProcessedAtUtc);
    }

    [Fact]
    public async Task Webhook_ReplayedDeliveryDoesNotCreateDuplicateAuditRecord()
    {
        await using var factory = CreateEnabledFactory();
        using var client = CreateClient(factory);
        var payload = CreatePayload("opened");

        using var firstRequest = CreateRequest(
            "delivery-replayed",
            payload,
            CreateSignature(payload));
        using var replayedRequest = CreateRequest(
            "delivery-replayed",
            payload,
            CreateSignature(payload));

        var firstResponse = await client.SendAsync(firstRequest);
        var replayedResponse = await client.SendAsync(replayedRequest);

        Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, replayedResponse.StatusCode);
        var replayedBody = await replayedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Duplicate", replayedBody.GetProperty("outcome").GetString());
        Assert.Equal(1, await CountDeliveriesAsync(factory, "delivery-replayed"));
    }

    [Fact]
    public async Task Webhook_AuditsUnsupportedEventWithoutQueueingIt()
    {
        await using var factory = CreateEnabledFactory();
        using var client = CreateClient(factory);
        var payload = CreatePayload(action: null);
        using var request = CreateRequest(
            "delivery-unsupported",
            payload,
            CreateSignature(payload),
            eventName: "issues");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ignored", body.GetProperty("outcome").GetString());
        var delivery = await FindDeliveryAsync(factory, "delivery-unsupported");
        Assert.NotNull(delivery);
        Assert.Equal(GitHubWebhookDeliveryStatus.Ignored, delivery.Status);
        Assert.Equal(0, delivery.AttemptCount);
    }

    [Fact]
    public async Task Webhook_ReturnsServiceUnavailableWhenIntegrationIsDisabled()
    {
        using var client = CreateClient(_factory);
        var payload = CreatePayload("opened");
        using var request = CreateRequest(
            "delivery-disabled",
            payload,
            CreateSignature(payload));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateEnabledFactory() =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Integrations:GitHub:Webhook:Enabled", "true");
            builder.UseSetting("Integrations:GitHub:Webhook:Secret", WebhookSecret);
            builder.UseSetting("Integrations:GitHub:Webhook:MaximumPayloadBytes", "2097152");
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

    private static HttpRequestMessage CreateRequest(
        string deliveryId,
        byte[] payload,
        string signature,
        string eventName = "pull_request")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath)
        {
            Content = new ByteArrayContent(payload),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-GitHub-Delivery", deliveryId);
        request.Headers.Add("X-GitHub-Event", eventName);
        request.Headers.Add("X-Hub-Signature-256", signature);
        return request;
    }

    private static byte[] CreatePayload(string? action) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            action,
            repository = new
            {
                id = 123456789,
                full_name = "Btrung-uit/TeamPulse",
            },
        });

    private static string CreateSignature(byte[] payload)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookSecret), payload);
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static async Task<int> CountDeliveriesAsync(
        WebApplicationFactory<Program> factory,
        string deliveryId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
        return await dbContext.GitHubWebhookDeliveries
            .CountAsync(delivery => delivery.DeliveryId == deliveryId);
    }

    private static async Task<GitHubWebhookDelivery> WaitForDeliveryAsync(
        WebApplicationFactory<Program> factory,
        string deliveryId)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var delivery = await FindDeliveryAsync(factory, deliveryId);
            if (delivery?.Status == GitHubWebhookDeliveryStatus.Processed)
            {
                return delivery;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"GitHub delivery '{deliveryId}' was not processed.");
    }

    private static async Task<GitHubWebhookDelivery?> FindDeliveryAsync(
        WebApplicationFactory<Program> factory,
        string deliveryId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
        return await dbContext.GitHubWebhookDeliveries
            .AsNoTracking()
            .SingleOrDefaultAsync(delivery => delivery.DeliveryId == deliveryId);
    }
}
