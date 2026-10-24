using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Integrations;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class GitHubWebhookDeliveryConfiguration
    : IEntityTypeConfiguration<GitHubWebhookDelivery>
{
    public void Configure(EntityTypeBuilder<GitHubWebhookDelivery> builder)
    {
        builder.ToTable("GitHubWebhookDeliveries");
        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.Id).ValueGeneratedNever();
        builder.Property(delivery => delivery.DeliveryId)
            .HasMaxLength(GitHubWebhookDelivery.DeliveryIdMaximumLength)
            .IsRequired();
        builder.Property(delivery => delivery.EventName)
            .HasMaxLength(GitHubWebhookDelivery.EventNameMaximumLength)
            .IsRequired();
        builder.Property(delivery => delivery.Action)
            .HasMaxLength(GitHubWebhookDelivery.ActionMaximumLength);
        builder.Property(delivery => delivery.RepositoryFullName)
            .HasMaxLength(GitHubWebhookDelivery.RepositoryNameMaximumLength);
        builder.Property(delivery => delivery.Payload).IsRequired();
        builder.Property(delivery => delivery.PayloadSha256)
            .HasMaxLength(GitHubWebhookDelivery.PayloadHashMaximumLength)
            .IsRequired();
        builder.Property(delivery => delivery.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(delivery => delivery.Error)
            .HasMaxLength(GitHubWebhookDelivery.ErrorMaximumLength);

        builder.HasIndex(delivery => delivery.DeliveryId).IsUnique();
        builder.HasIndex(delivery => new
        {
            delivery.Status,
            delivery.CreatedAtUtc,
        });
        builder.ConfigureAuditProperties();
    }
}
