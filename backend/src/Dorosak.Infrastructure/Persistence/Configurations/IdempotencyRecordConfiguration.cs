using Dorosak.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records", "operations", table =>
        {
            table.HasCheckConstraint("ck_idempotency_records_expiration", "expires_at > created_at");
            table.HasCheckConstraint("ck_idempotency_records_response_schema_version", "response_schema_version > 0");
        });

        builder.HasKey(record => record.Id).HasName("pk_idempotency_records");
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.Property(record => record.Scope).HasMaxLength(200).IsRequired();
        builder.Property(record => record.Operation).HasMaxLength(400).IsRequired();
        builder.Property(record => record.Key).HasMaxLength(200).IsRequired();
        builder.Property(record => record.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(record => record.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(record => record.ResponsePayload).HasColumnType("jsonb").IsRequired();
        builder.Property(record => record.ResponseSchemaVersion).IsRequired();

        builder
            .HasIndex(record => new { record.Scope, record.Operation, record.Key })
            .IsUnique()
            .HasDatabaseName("uq_idempotency_records_scope_operation_key");
        builder
            .HasIndex(record => record.ExpiresAt)
            .HasDatabaseName("ix_idempotency_records_expires_at");
    }
}
