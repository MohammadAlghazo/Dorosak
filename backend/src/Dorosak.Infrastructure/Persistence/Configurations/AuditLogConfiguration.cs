using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs", "operations");
        builder.HasKey(audit => audit.Id).HasName("pk_audit_logs");
        builder.Property(audit => audit.Id).ValueGeneratedNever();
        builder.Property(audit => audit.Action).HasMaxLength(200).IsRequired();
        builder.Property(audit => audit.TargetType).HasMaxLength(100).IsRequired();
        builder.Property(audit => audit.Result).HasMaxLength(50).IsRequired();
        builder.Property(audit => audit.Reason).HasMaxLength(2000);
        builder.HasIndex(audit => new { audit.ActorUserId, audit.OccurredAt, audit.Id })
            .HasDatabaseName("ix_audit_logs_actor_occurred_id");
        builder.HasIndex(audit => new { audit.TargetType, audit.TargetId, audit.OccurredAt })
            .HasDatabaseName("ix_audit_logs_target_occurred");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(audit => audit.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_audit_logs_users_actor_user_id");
    }
}
