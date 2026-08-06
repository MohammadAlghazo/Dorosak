using Dorosak.Domain.Identity;
using Dorosak.Domain.Profiles;
using Dorosak.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        builder.ToTable("refresh_sessions", "identity", table =>
        {
            table.HasCheckConstraint("ck_refresh_sessions_expiration", "idle_expires_at <= absolute_expires_at");
            table.HasCheckConstraint("ck_refresh_sessions_authorization_version", "authorization_version > 0");
        });
        builder.HasKey(session => session.Id).HasName("pk_refresh_sessions");
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.FamilyId).IsRequired();
        builder.Property(session => session.DeviceName).HasMaxLength(300).IsRequired();
        builder.Property(session => session.IpAddressHash).HasMaxLength(64).IsRequired();
        builder.Property(session => session.AuthenticationMethods).HasMaxLength(100).IsRequired();
        builder.Property(session => session.RevocationReason).HasMaxLength(200);
        builder.HasIndex(session => session.UserId).HasDatabaseName("ix_refresh_sessions_user_id");
        builder.HasIndex(session => session.FamilyId).HasDatabaseName("ix_refresh_sessions_family_id");
        builder
            .HasIndex(session => new { session.UserId, session.RevokedAt, session.AbsoluteExpiresAt })
            .HasDatabaseName("ix_refresh_sessions_active_user");
        builder
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_refresh_sessions_users_user_id");
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", "identity", table =>
        {
            table.HasCheckConstraint("ck_refresh_tokens_expiration", "expires_at > created_at");
        });
        builder.HasKey(token => token.Id).HasName("pk_refresh_tokens");
        builder.Property(token => token.Id).ValueGeneratedNever();
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(token => token.RevocationReason).HasMaxLength(200);
        builder
            .HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("uq_refresh_tokens_token_hash");
        builder.HasIndex(token => token.SessionId).HasDatabaseName("ix_refresh_tokens_session_id");
        builder.HasIndex(token => token.FamilyId).HasDatabaseName("ix_refresh_tokens_family_id");
        builder
            .HasOne<RefreshSession>()
            .WithMany()
            .HasForeignKey(token => token.SessionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_refresh_tokens_sessions_session_id");
    }
}

internal sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.ToTable("security_events", "identity");
        builder.HasKey(securityEvent => securityEvent.Id).HasName("pk_security_events");
        builder.Property(securityEvent => securityEvent.Id).ValueGeneratedNever();
        builder.Property(securityEvent => securityEvent.Type).HasMaxLength(200).IsRequired();
        builder.Property(securityEvent => securityEvent.IpAddressHash).HasMaxLength(64);
        builder.Property(securityEvent => securityEvent.Metadata).HasColumnType("jsonb");
        builder
            .HasIndex(securityEvent => new { securityEvent.UserId, securityEvent.OccurredAt })
            .HasDatabaseName("ix_security_events_user_occurred_at");
        builder.HasIndex(securityEvent => securityEvent.SessionId).HasDatabaseName("ix_security_events_session_id");
        builder
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(securityEvent => securityEvent.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_security_events_users_user_id");
    }
}

internal sealed class MfaChallengeConfiguration : IEntityTypeConfiguration<MfaChallenge>
{
    public void Configure(EntityTypeBuilder<MfaChallenge> builder)
    {
        builder.ToTable("mfa_challenges", "identity", table =>
        {
            table.HasCheckConstraint("ck_mfa_challenges_expiration", "expires_at > created_at");
            table.HasCheckConstraint("ck_mfa_challenges_attempt_count", "attempt_count >= 0");
        });
        builder.HasKey(challenge => challenge.Id).HasName("pk_mfa_challenges");
        builder.Property(challenge => challenge.Id).ValueGeneratedNever();
        builder.Property(challenge => challenge.TokenHash).HasMaxLength(64).IsRequired();
        builder
            .HasIndex(challenge => challenge.TokenHash)
            .IsUnique()
            .HasDatabaseName("uq_mfa_challenges_token_hash");
        builder.HasIndex(challenge => challenge.UserId).HasDatabaseName("ix_mfa_challenges_user_id");
        builder
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(challenge => challenge.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_mfa_challenges_users_user_id");
    }
}

internal sealed class MfaRecoveryCodeConfiguration : IEntityTypeConfiguration<MfaRecoveryCode>
{
    public void Configure(EntityTypeBuilder<MfaRecoveryCode> builder)
    {
        builder.ToTable("mfa_recovery_codes", "identity");
        builder.HasKey(code => code.Id).HasName("pk_mfa_recovery_codes");
        builder.Property(code => code.Id).ValueGeneratedNever();
        builder.Property(code => code.CodeHash).HasMaxLength(64).IsRequired();
        builder
            .HasIndex(code => new { code.UserId, code.CodeHash })
            .IsUnique()
            .HasDatabaseName("uq_mfa_recovery_codes_user_hash");
        builder
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_mfa_recovery_codes_users_user_id");
    }
}

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("profiles", "profiles");
        builder.HasKey(profile => profile.UserId).HasName("pk_profiles");
        builder.Property(profile => profile.UserId).ValueGeneratedNever();
        builder.Property(profile => profile.DisplayName).HasMaxLength(100).IsRequired();
        builder
            .HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<UserProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_profiles_users_user_id");
    }
}
