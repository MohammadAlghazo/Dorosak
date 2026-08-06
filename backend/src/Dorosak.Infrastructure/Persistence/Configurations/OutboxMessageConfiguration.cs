using Dorosak.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", "operations", table =>
        {
            table.HasCheckConstraint("ck_outbox_messages_schema_version", "schema_version > 0");
            table.HasCheckConstraint("ck_outbox_messages_attempt_count", "attempt_count >= 0");
        });

        builder.HasKey(message => message.Id).HasName("pk_outbox_messages");
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.EventType).HasMaxLength(300).IsRequired();
        builder.Property(message => message.SchemaVersion).IsRequired();
        builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.Headers).HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.LastErrorCode).HasMaxLength(200);

        builder
            .HasIndex(message => new { message.AvailableAt, message.OccurredAt, message.Id })
            .HasDatabaseName("ix_outbox_messages_pending")
            .HasFilter("processed_at IS NULL");
        builder
            .HasIndex(message => message.LockedUntil)
            .HasDatabaseName("ix_outbox_messages_locked_until")
            .HasFilter("processed_at IS NULL AND locked_until IS NOT NULL");
    }
}
