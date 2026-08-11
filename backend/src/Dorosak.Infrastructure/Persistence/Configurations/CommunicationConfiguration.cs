using Dorosak.Domain.Catalog;
using Dorosak.Domain.Communications;
using Dorosak.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations", "communication", table =>
            table.HasCheckConstraint("ck_conversations_last_sequence", "last_sequence >= 0"));
        builder.HasKey(conversation => conversation.Id).HasName("pk_conversations");
        builder.Property(conversation => conversation.Id).ValueGeneratedNever();
        builder.Property(conversation => conversation.CourseId).IsRequired();
        builder.Property(conversation => conversation.LastSequence).HasDefaultValue(0L);
        builder.HasIndex(conversation => new { conversation.UpdatedAt, conversation.Id })
            .IsDescending()
            .HasDatabaseName("ix_conversations_updated_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(conversation => conversation.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_conversations_users_created_by_user_id");
        builder.HasOne<Course>().WithMany().HasForeignKey(conversation => conversation.CourseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_conversations_courses_course_id");
    }
}

internal sealed class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.ToTable("conversation_participants", "communication", table =>
        {
            table.HasCheckConstraint(
                "ck_conversation_participants_left_at",
                "left_at IS NULL OR left_at >= joined_at");
        });
        builder.HasKey(participant => new { participant.ConversationId, participant.UserId })
            .HasName("pk_conversation_participants");
        builder.Ignore(participant => participant.IsCurrent);
        builder.HasIndex(participant => new { participant.UserId, participant.ConversationId })
            .HasFilter("left_at IS NULL")
            .HasDatabaseName("ix_conversation_participants_current_user_conversation");
        builder.HasOne<Conversation>().WithMany().HasForeignKey(participant => participant.ConversationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_conversation_participants_conversations_conversation_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(participant => participant.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_conversation_participants_users_user_id");
    }
}

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages", "communication", table =>
        {
            table.HasCheckConstraint(
                "ck_messages_body",
                "char_length(btrim(body)) BETWEEN 1 AND 5000");
            table.HasCheckConstraint("ck_messages_sequence", "sequence > 0");
        });
        builder.HasKey(message => message.Id).HasName("pk_messages");
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.SenderUserId).HasColumnName("sender_id");
        builder.Property(message => message.Body).HasMaxLength(Message.MaximumBodyLength).IsRequired();
        builder.HasIndex(message => new { message.ConversationId, message.Sequence })
            .IsUnique()
            .HasDatabaseName("uq_messages_conversation_sequence");
        builder.HasIndex(message => new
        {
            message.ConversationId,
            message.SenderUserId,
            message.ClientMessageId,
        })
            .IsUnique()
            .HasDatabaseName("uq_messages_conversation_sender_client_message");
        builder.HasIndex(message => new { message.ConversationId, message.CreatedAt, message.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_messages_conversation_created_id");
        builder.HasOne<Conversation>().WithMany().HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_messages_conversations_conversation_id");
        builder.HasOne<ConversationParticipant>().WithMany()
            .HasForeignKey(message => new { message.ConversationId, message.SenderUserId })
            .HasPrincipalKey(participant => new { participant.ConversationId, participant.UserId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_messages_participants_conversation_sender_id");
    }
}

internal sealed class NotificationSequenceConfiguration : IEntityTypeConfiguration<NotificationSequence>
{
    public void Configure(EntityTypeBuilder<NotificationSequence> builder)
    {
        builder.ToTable("notification_sequences", "communication", table =>
            table.HasCheckConstraint("ck_notification_sequences_last_sequence", "last_sequence >= 0"));
        builder.HasKey(sequence => sequence.UserId).HasName("pk_notification_sequences");
        builder.Property(sequence => sequence.LastSequence).HasDefaultValue(0L);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(sequence => sequence.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notification_sequences_users_user_id");
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", "communication", table =>
        {
            table.HasCheckConstraint("ck_notifications_sequence", "sequence > 0");
            table.HasCheckConstraint("ck_notifications_read_state", "is_read = (read_at IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_notifications_target_projection",
                "(message_id IS NOT NULL AND announcement_id IS NULL AND announcement_version IS NULL AND title IS NULL AND body IS NULL) OR " +
                "(message_id IS NULL AND announcement_id IS NOT NULL AND announcement_version > 0 AND " +
                "char_length(btrim(title)) BETWEEN 1 AND 200 AND char_length(btrim(body)) BETWEEN 1 AND 10000)");
        });
        builder.HasKey(notification => notification.Id).HasName("pk_notifications");
        builder.Property(notification => notification.Id).ValueGeneratedNever();
        builder.Property(notification => notification.Title).HasMaxLength(Announcement.MaximumTitleLength);
        builder.Property(notification => notification.Body).HasMaxLength(Announcement.MaximumBodyLength);
        builder.HasAlternateKey(notification => new { notification.Id, notification.UserId })
            .HasName("ak_notifications_id_user_id");
        builder.HasIndex(notification => new { notification.UserId, notification.Sequence })
            .IsUnique()
            .HasDatabaseName("uq_notifications_user_sequence");
        builder.HasIndex(notification => new
        {
            notification.UserId,
            notification.IsRead,
            notification.CreatedAt,
            notification.Id,
        })
            .IsDescending(false, false, true, true)
            .HasDatabaseName("ix_notifications_user_read_created_id");
        builder.HasIndex(notification => notification.MessageId)
            .HasDatabaseName("ix_notifications_message_id");
        builder.HasIndex(notification => notification.AnnouncementId)
            .HasDatabaseName("ix_notifications_announcement_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notifications_users_user_id");
        builder.HasOne<Message>().WithMany().HasForeignKey(notification => notification.MessageId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notifications_messages_message_id");
        builder.HasOne<Announcement>().WithMany().HasForeignKey(notification => notification.AnnouncementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notifications_announcements_announcement_id");
    }
}

internal sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("announcements", "communication", table =>
        {
            table.HasCheckConstraint(
                "ck_announcements_title",
                "char_length(btrim(title)) BETWEEN 1 AND 200");
            table.HasCheckConstraint(
                "ck_announcements_body",
                "char_length(btrim(body)) BETWEEN 1 AND 10000");
            table.HasCheckConstraint("ck_announcements_version", "version > 0");
            table.HasCheckConstraint(
                "ck_announcements_deleted",
                "(deleted_at IS NULL AND deleted_by_user_id IS NULL) OR " +
                "(deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL)");
        });
        builder.HasKey(announcement => announcement.Id).HasName("pk_announcements");
        builder.Property(announcement => announcement.Id).ValueGeneratedNever();
        builder.Property(announcement => announcement.Title)
            .HasMaxLength(Announcement.MaximumTitleLength)
            .IsRequired();
        builder.Property(announcement => announcement.Body)
            .HasMaxLength(Announcement.MaximumBodyLength)
            .IsRequired();
        builder.HasIndex(announcement => new { announcement.CourseId, announcement.CreatedAt, announcement.Id })
            .IsDescending(false, true, true)
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ix_announcements_course_created_id");
        builder.HasOne<Course>().WithMany().HasForeignKey(announcement => announcement.CourseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_announcements_courses_course_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(announcement => announcement.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_announcements_users_created_by_user_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(announcement => announcement.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_announcements_users_deleted_by_user_id");
    }
}

internal sealed class AnnouncementTargetConfiguration : IEntityTypeConfiguration<AnnouncementTarget>
{
    public void Configure(EntityTypeBuilder<AnnouncementTarget> builder)
    {
        builder.ToTable("announcement_targets", "communication", table =>
            table.HasCheckConstraint("ck_announcement_targets_version", "announcement_version > 0"));
        builder.HasKey(target => new { target.AnnouncementId, target.UserId, target.AnnouncementVersion })
            .HasName("pk_announcement_targets");
        builder.HasIndex(target => target.NotificationId)
            .IsUnique()
            .HasDatabaseName("uq_announcement_targets_notification_id");
        builder.HasIndex(target => new { target.UserId, target.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_announcement_targets_user_created_at");
        builder.HasOne<Announcement>().WithMany().HasForeignKey(target => target.AnnouncementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_announcement_targets_announcements_announcement_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(target => target.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_announcement_targets_users_user_id");
        builder.HasOne<Notification>().WithMany()
            .HasForeignKey(target => new { target.NotificationId, target.UserId })
            .HasPrincipalKey(notification => new { notification.Id, notification.UserId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_announcement_targets_notifications_notification_user");
    }
}
