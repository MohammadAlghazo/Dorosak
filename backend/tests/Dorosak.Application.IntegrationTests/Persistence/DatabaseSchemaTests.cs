using Dorosak.Domain.Communications;
using Dorosak.Domain.Engagement;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dorosak.Application.IntegrationTests.Persistence;

[Collection(InfrastructureTestGroup.Name)]
public sealed class DatabaseSchemaTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Migration_CreatesExpectedOperationalAndIdentitySchemas()
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        IEnumerable<string> pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(
            TestContext.Current.CancellationToken);
        long expectedMigrationCount = dbContext.Database.GetMigrations().LongCount();

        Assert.Empty(pendingMigrations);

        await using var connection = new NpgsqlConnection(fixture.DatabaseConnection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expectedMigrationCount, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM migrations.__ef_migrations_history",
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'operations' AND table_name IN ('outbox_messages', 'idempotency_records')",
            TestContext.Current.CancellationToken));
        Assert.Equal(7L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'engagement' AND table_name IN ('course_reviews', 'discussion_threads', 'comments', 'comment_likes', 'content_reports', 'moderation_cases', 'moderation_actions')",
            TestContext.Current.CancellationToken));
        Assert.Equal(7L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'communication' AND table_name IN ('conversations', 'conversation_participants', 'messages', 'notification_sequences', 'notifications', 'announcements', 'announcement_targets')",
            TestContext.Current.CancellationToken));
        Assert.Equal(6L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'media' AND table_name IN ('upload_sessions', 'upload_parts', 'media_assets', 'media_variants', 'caption_tracks', 'media_processing_jobs')",
            TestContext.Current.CancellationToken));
        Assert.Equal(12L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'identity' AND table_name IN ('users', 'roles', 'user_roles', 'user_claims', 'role_claims', 'user_logins', 'user_tokens', 'refresh_sessions', 'refresh_tokens', 'security_events', 'mfa_challenges', 'mfa_recovery_codes')",
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'profiles' AND table_name = 'profiles'",
            TestContext.Current.CancellationToken));
        Assert.Equal(3L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.schemata WHERE schema_name IN ('catalog', 'authoring', 'media')",
            TestContext.Current.CancellationToken));
        Assert.Equal(10L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'catalog' AND table_name IN ('courses', 'course_localizations', 'course_slugs', 'course_instructors', 'categories', 'category_localizations', 'course_categories', 'tags', 'tag_localizations', 'course_tags')",
            TestContext.Current.CancellationToken));
        Assert.Equal(6L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'authoring' AND table_name IN ('course_drafts', 'sections', 'section_revisions', 'lessons', 'lesson_revisions', 'publication_reviews')",
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'profiles' AND table_name IN ('teacher_profiles', 'teacher_applications')",
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'catalog' AND table_name IN ('course_releases', 'catalog_documents')",
            TestContext.Current.CancellationToken));
        Assert.Equal(7L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'learning' AND table_name IN ('entitlements', 'enrollments', 'lesson_progress', 'course_completions', 'bookmarks', 'notes', 'recently_viewed')",
            TestContext.Current.CancellationToken));
        Assert.Equal(14L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'assessment' AND table_name IN ('quizzes', 'quiz_versions', 'questions', 'question_options', 'quiz_attempts', 'quiz_answers', 'assignments', 'assignment_versions', 'assignment_submissions', 'assignment_audience_members', 'quiz_audience_members', 'submission_files', 'grade_revisions', 'quiz_grade_revisions')",
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'commerce' AND table_name IN ('demo_orders', 'demo_payments')",
            TestContext.Current.CancellationToken));
        Assert.Equal(4L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM catalog.categories",
            TestContext.Current.CancellationToken));
        Assert.Equal(8L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM catalog.category_localizations",
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM pg_extension WHERE extname IN ('pg_trgm', 'unaccent')",
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'operations' AND table_name = 'data_protection_keys'",
            TestContext.Current.CancellationToken));
        Assert.Equal(0L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_name LIKE 'AspNet%'",
            TestContext.Current.CancellationToken));
        Assert.True(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'engagement.course_reviews', 'SELECT,INSERT,UPDATE')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'engagement.course_reviews', 'DELETE,TRUNCATE')",
            TestContext.Current.CancellationToken));
        Assert.True(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'engagement.discussion_threads', 'SELECT') AND has_table_privilege('dorosak_runtime', 'engagement.discussion_threads', 'INSERT') AND has_table_privilege('dorosak_runtime', 'engagement.discussion_threads', 'UPDATE')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'engagement.discussion_threads', 'DELETE,TRUNCATE')",
            TestContext.Current.CancellationToken));
        Assert.True(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'engagement.comments', 'SELECT') AND has_table_privilege('dorosak_runtime', 'engagement.comments', 'INSERT') AND has_table_privilege('dorosak_runtime', 'engagement.comments', 'UPDATE')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'engagement.comments', 'DELETE,TRUNCATE')",
            TestContext.Current.CancellationToken));
        Assert.True(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'engagement.comment_likes', 'SELECT') AND has_table_privilege('dorosak_runtime', 'engagement.comment_likes', 'INSERT') AND has_table_privilege('dorosak_runtime', 'engagement.comment_likes', 'DELETE')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'engagement.comment_likes', 'UPDATE,TRUNCATE')",
            TestContext.Current.CancellationToken));
        Assert.True(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'engagement.content_reports', 'SELECT') AND has_table_privilege('dorosak_runtime', 'engagement.content_reports', 'INSERT') AND has_table_privilege('dorosak_runtime', 'engagement.moderation_cases', 'SELECT') AND has_table_privilege('dorosak_runtime', 'engagement.moderation_cases', 'INSERT')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'engagement.content_reports', 'UPDATE') OR has_table_privilege('dorosak_runtime', 'engagement.content_reports', 'DELETE') OR has_table_privilege('dorosak_runtime', 'engagement.content_reports', 'TRUNCATE') OR has_table_privilege('dorosak_runtime', 'engagement.moderation_cases', 'UPDATE') OR has_table_privilege('dorosak_runtime', 'engagement.moderation_cases', 'DELETE') OR has_table_privilege('dorosak_runtime', 'engagement.moderation_cases', 'TRUNCATE')",
            TestContext.Current.CancellationToken));
        Assert.True(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_column_privilege('dorosak_runtime', 'engagement.content_reports', 'status', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'engagement.content_reports', 'updated_at', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'engagement.content_reports', 'closed_at', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'engagement.moderation_cases', 'status', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'engagement.moderation_cases', 'assigned_to_user_id', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'engagement.moderation_cases', 'version', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'engagement.moderation_cases', 'updated_at', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'engagement.moderation_cases', 'closed_at', 'UPDATE')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_column_privilege('dorosak_runtime', 'engagement.content_reports', 'reporter_user_id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'engagement.content_reports', 'course_id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'engagement.content_reports', 'reason', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'engagement.content_reports', 'details', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'engagement.moderation_cases', 'report_id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'engagement.moderation_cases', 'created_at', 'UPDATE')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_column_privilege('dorosak_runtime', 'engagement.content_reports', 'message_id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'engagement.content_reports', 'message_body_snapshot', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'engagement.content_reports', 'message_sender_user_id_snapshot', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'engagement.content_reports', 'message_sender_name_snapshot', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'engagement.content_reports', 'message_course_id_snapshot', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'engagement.content_reports', 'message_course_title_snapshot', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'engagement.content_reports', 'message_conversation_id_snapshot', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'engagement.content_reports', 'message_sequence_snapshot', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'engagement.content_reports', 'message_created_at_snapshot', 'UPDATE')",
            TestContext.Current.CancellationToken));
        Assert.True(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'engagement.moderation_actions', 'SELECT') AND has_table_privilege('dorosak_runtime', 'engagement.moderation_actions', 'INSERT')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'engagement.moderation_actions', 'UPDATE,DELETE,TRUNCATE')",
            TestContext.Current.CancellationToken));
        Assert.True(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_schema_privilege('dorosak_runtime', 'communication', 'USAGE') AND has_table_privilege('dorosak_runtime', 'communication.conversations', 'SELECT,INSERT') AND has_table_privilege('dorosak_runtime', 'communication.conversation_participants', 'SELECT,INSERT') AND has_table_privilege('dorosak_runtime', 'communication.messages', 'SELECT,INSERT') AND has_table_privilege('dorosak_runtime', 'communication.notification_sequences', 'SELECT,INSERT') AND has_table_privilege('dorosak_runtime', 'communication.notifications', 'SELECT,INSERT') AND has_table_privilege('dorosak_runtime', 'communication.announcements', 'SELECT,INSERT') AND has_table_privilege('dorosak_runtime', 'communication.announcement_targets', 'SELECT,INSERT')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'communication.conversations', 'UPDATE,DELETE,TRUNCATE') OR has_table_privilege('dorosak_runtime', 'communication.conversation_participants', 'UPDATE,DELETE,TRUNCATE') OR has_table_privilege('dorosak_runtime', 'communication.messages', 'UPDATE,DELETE,TRUNCATE') OR has_table_privilege('dorosak_runtime', 'communication.notification_sequences', 'UPDATE,DELETE,TRUNCATE') OR has_table_privilege('dorosak_runtime', 'communication.notifications', 'UPDATE,DELETE,TRUNCATE') OR has_table_privilege('dorosak_runtime', 'communication.announcements', 'UPDATE,DELETE,TRUNCATE') OR has_table_privilege('dorosak_runtime', 'communication.announcement_targets', 'UPDATE,DELETE,TRUNCATE')",
            TestContext.Current.CancellationToken));
        Assert.True(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_column_privilege('dorosak_runtime', 'communication.conversations', 'updated_at', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'communication.conversations', 'last_sequence', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'communication.notification_sequences', 'last_sequence', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'communication.notifications', 'is_read', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'communication.notifications', 'read_at', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'communication.announcements', 'title', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'communication.announcements', 'body', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'communication.announcements', 'version', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'communication.announcements', 'updated_at', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'communication.announcements', 'deleted_at', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'communication.announcements', 'deleted_by_user_id', 'UPDATE') AND has_column_privilege('dorosak_runtime', 'communication.conversation_participants', 'left_at', 'UPDATE')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_column_privilege('dorosak_runtime', 'communication.conversations', 'id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.conversations', 'created_by_user_id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.messages', 'sequence', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.notification_sequences', 'user_id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.notifications', 'user_id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.notifications', 'sequence', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.notifications', 'message_id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.notifications', 'announcement_id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.notifications', 'announcement_version', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.notifications', 'target_announcement_id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.notifications', 'target_announcement_version', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.notifications', 'title', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.notifications', 'body', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.announcements', 'course_id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.announcements', 'created_by_user_id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.announcement_targets', 'user_id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.announcement_targets', 'notification_id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.conversation_participants', 'conversation_id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.conversation_participants', 'user_id', 'UPDATE') OR has_column_privilege('dorosak_runtime', 'communication.conversation_participants', 'joined_at', 'UPDATE')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'communication.conversation_participants', 'UPDATE')",
            TestContext.Current.CancellationToken));
        string reportTargetConstraint = await ExecuteScalarAsync<string>(
            connection,
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_content_reports_exact_target'",
            TestContext.Current.CancellationToken);
        Assert.Contains(
            "num_nonnulls(course_id, review_id, comment_id, reported_user_id, message_id) = 1",
            reportTargetConstraint,
            StringComparison.Ordinal);
        string reportMessageSnapshotConstraint = await ExecuteScalarAsync<string>(
            connection,
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_content_reports_message_snapshot'",
            TestContext.Current.CancellationToken);
        Assert.Contains("message_id IS NOT NULL", reportMessageSnapshotConstraint, StringComparison.Ordinal);
        Assert.Contains("message_sender_user_id_snapshot <> reporter_user_id", reportMessageSnapshotConstraint, StringComparison.Ordinal);
        Assert.Contains("message_sequence_snapshot > 0", reportMessageSnapshotConstraint, StringComparison.Ordinal);
        string reportMessageForeignKey = await ExecuteScalarAsync<string>(
            connection,
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'fk_content_reports_messages_message_id'",
            TestContext.Current.CancellationToken);
        Assert.Contains(
            "FOREIGN KEY (message_id) REFERENCES communication.messages(id)",
            reportMessageForeignKey,
            StringComparison.Ordinal);
        Assert.Equal(9L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'engagement' AND table_name = 'content_reports' AND column_name IN ('message_id', 'message_body_snapshot', 'message_sender_user_id_snapshot', 'message_sender_name_snapshot', 'message_course_id_snapshot', 'message_course_title_snapshot', 'message_conversation_id_snapshot', 'message_sequence_snapshot', 'message_created_at_snapshot') AND is_nullable = 'YES'",
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM pg_indexes WHERE schemaname = 'engagement' AND indexname = 'uq_content_reports_open_message' AND indexdef LIKE '%UNIQUE%' AND indexdef LIKE '%message_id IS NOT NULL%'",
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM pg_indexes WHERE schemaname = 'engagement' AND indexname IN ('ix_content_reports_created_id', 'ix_moderation_cases_created_id')",
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM pg_indexes WHERE schemaname = 'communication' AND indexname = 'uq_messages_conversation_sender_client_message' AND indexdef LIKE '%UNIQUE%'",
            TestContext.Current.CancellationToken));
        Assert.Equal(2L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM pg_indexes WHERE schemaname = 'communication' AND indexname IN ('uq_messages_conversation_sequence', 'uq_notifications_user_sequence') AND indexdef LIKE '%UNIQUE%'",
            TestContext.Current.CancellationToken));
        string messageParticipantConstraint = await ExecuteScalarAsync<string>(
            connection,
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'fk_messages_participants_conversation_sender_id'",
            TestContext.Current.CancellationToken);
        Assert.Contains("FOREIGN KEY (conversation_id, sender_id)", messageParticipantConstraint, StringComparison.Ordinal);
        string notificationProjectionConstraint = await ExecuteScalarAsync<string>(
            connection,
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_notifications_target_projection'",
            TestContext.Current.CancellationToken);
        Assert.Contains("message_id IS NOT NULL", notificationProjectionConstraint, StringComparison.Ordinal);
        Assert.Contains("announcement_id IS NOT NULL", notificationProjectionConstraint, StringComparison.Ordinal);
        Assert.Contains("announcement_version IS NOT NULL", notificationProjectionConstraint, StringComparison.Ordinal);
        Assert.Contains("title IS NOT NULL", notificationProjectionConstraint, StringComparison.Ordinal);
        Assert.Contains("body IS NOT NULL", notificationProjectionConstraint, StringComparison.Ordinal);
        string targetOwnershipConstraint = await ExecuteScalarAsync<string>(
            connection,
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'fk_announcement_targets_notifications_projection'",
            TestContext.Current.CancellationToken);
        Assert.Contains(
            "FOREIGN KEY (notification_id, user_id, announcement_id, announcement_version)",
            targetOwnershipConstraint,
            StringComparison.Ordinal);
        Assert.Contains(
            "REFERENCES communication.notifications(id, user_id, target_announcement_id, target_announcement_version)",
            targetOwnershipConstraint,
            StringComparison.Ordinal);
        Assert.Equal(2L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'communication' AND table_name = 'notifications' AND column_name IN ('target_announcement_id', 'target_announcement_version') AND is_nullable = 'NO'",
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'engagement' AND table_name = 'discussion_threads' AND column_name = 'edited_at'",
            TestContext.Current.CancellationToken));
        Assert.Equal("NO", await ExecuteScalarAsync<string>(
            connection,
            "SELECT is_nullable FROM information_schema.columns WHERE table_schema = 'communication' AND table_name = 'conversations' AND column_name = 'course_id'",
            TestContext.Current.CancellationToken));
        string commentDepthConstraint = await ExecuteScalarAsync<string>(
            connection,
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_comments_depth'",
            TestContext.Current.CancellationToken);
        Assert.Contains("depth >= 0", commentDepthConstraint, StringComparison.Ordinal);
        Assert.Contains("depth <= 2", commentDepthConstraint, StringComparison.Ordinal);
        Assert.Equal(3L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM identity.roles",
            TestContext.Current.CancellationToken));
        Assert.True(await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM identity.role_claims WHERE claim_type = 'permission'",
            TestContext.Current.CancellationToken) >= 50);
        Assert.Equal("jsonb", await ExecuteScalarAsync<string>(
            connection,
            "SELECT data_type FROM information_schema.columns WHERE table_schema = 'operations' AND table_name = 'outbox_messages' AND column_name = 'payload'",
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'media' AND table_name = 'media_variants' AND column_name = 'sha256'",
            TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'media' AND table_name = 'caption_tracks' AND column_name = 'source_media_asset_id'",
            TestContext.Current.CancellationToken));
        string compatibilityRange = await ExecuteScalarAsync<string>(
            connection,
            "SELECT minimum_compatible_migration_id || '|' || maximum_compatible_migration_id FROM operations.schema_compatibility WHERE singleton",
            TestContext.Current.CancellationToken);
        string[] boundaries = compatibilityRange.Split('|', 2);
        Assert.Equal("20260811143212_Phase9CommunicationsConsistency", boundaries[0]);
        Assert.Equal(dbContext.Database.GetMigrations().Last(), boundaries[1]);
        Assert.True(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_schema_privilege('dorosak_runtime', 'media', 'USAGE')",
            TestContext.Current.CancellationToken));
        Assert.True(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'media.media_assets', 'SELECT,INSERT,UPDATE,DELETE')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'operations.audit_logs', 'UPDATE,DELETE,TRUNCATE')",
            TestContext.Current.CancellationToken));
        Assert.True(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_schema_privilege('dorosak_runtime', 'commerce', 'USAGE')",
            TestContext.Current.CancellationToken));
        Assert.True(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'commerce.demo_orders', 'SELECT,INSERT')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'commerce.demo_orders', 'UPDATE,DELETE,TRUNCATE')",
            TestContext.Current.CancellationToken));
        Assert.Equal(0L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'commerce' AND column_name IN ('card_number', 'pan', 'cvv', 'bank_account')",
            TestContext.Current.CancellationToken));
        Assert.True(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'assessment.submission_files', 'SELECT,INSERT')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'assessment.submission_files', 'UPDATE,DELETE,TRUNCATE')",
            TestContext.Current.CancellationToken));
        Assert.True(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'assessment.quiz_grade_revisions', 'SELECT,INSERT')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'assessment.quiz_grade_revisions', 'UPDATE,DELETE,TRUNCATE')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'assessment.grade_revisions', 'UPDATE,DELETE,TRUNCATE')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_table_privilege('dorosak_runtime', 'catalog.course_release_localizations', 'UPDATE,DELETE,TRUNCATE')",
            TestContext.Current.CancellationToken));
        Assert.True(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_column_privilege('dorosak_runtime', 'catalog.course_releases', 'state', 'UPDATE')",
            TestContext.Current.CancellationToken));
        Assert.False(await ExecuteScalarAsync<bool>(
            connection,
            "SELECT has_column_privilege('dorosak_runtime', 'catalog.course_releases', 'manifest_hash', 'UPDATE')",
            TestContext.Current.CancellationToken));

        string pendingIndex = await ExecuteScalarAsync<string>(
            connection,
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'operations' AND indexname = 'ix_outbox_messages_pending'",
            TestContext.Current.CancellationToken);
        Assert.Contains("processed_at IS NULL", pendingIndex, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContentReportModel_EncodesMessageTargetSnapshotAndActiveDedupe()
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        IEntityType report = Assert.IsAssignableFrom<IEntityType>(
            dbContext.Model.FindEntityType(typeof(ContentReport)));

        Assert.NotNull(report.FindProperty(nameof(ContentReport.MessageId)));
        Assert.Equal(5000, report.FindProperty(nameof(ContentReport.MessageBodySnapshot))?.GetMaxLength());
        Assert.Equal(100, report.FindProperty(nameof(ContentReport.MessageSenderNameSnapshot))?.GetMaxLength());
        Assert.Equal(200, report.FindProperty(nameof(ContentReport.MessageCourseTitleSnapshot))?.GetMaxLength());
        IForeignKey messageForeignKey = Assert.Single(report.GetForeignKeys(), foreignKey =>
            foreignKey.GetConstraintName() == "fk_content_reports_messages_message_id");
        Assert.Equal(typeof(Message), messageForeignKey.PrincipalEntityType.ClrType);
        Assert.Contains(report.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(ContentReport.ReporterUserId), nameof(ContentReport.MessageId) }));
    }

    [Fact]
    public async Task CommunicationModel_EncodesOwnershipSequenceAndContentBounds()
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        IEntityType conversation = Assert.IsAssignableFrom<IEntityType>(
            dbContext.Model.FindEntityType(typeof(Conversation)));
        IEntityType message = Assert.IsAssignableFrom<IEntityType>(
            dbContext.Model.FindEntityType(typeof(Message)));
        IEntityType notification = Assert.IsAssignableFrom<IEntityType>(
            dbContext.Model.FindEntityType(typeof(Notification)));
        IEntityType announcement = Assert.IsAssignableFrom<IEntityType>(
            dbContext.Model.FindEntityType(typeof(Announcement)));
        IEntityType target = Assert.IsAssignableFrom<IEntityType>(
            dbContext.Model.FindEntityType(typeof(AnnouncementTarget)));

        Assert.NotNull(conversation.FindProperty(nameof(Conversation.LastSequence)));
        Assert.False(conversation.FindProperty(nameof(Conversation.CourseId))!.IsNullable);
        Assert.NotNull(message.FindProperty(nameof(Message.Sequence)));
        Assert.Contains(message.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(Message.ConversationId), nameof(Message.Sequence) }));
        Assert.Contains(notification.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(Notification.UserId), nameof(Notification.Sequence) }));
        Assert.Equal(Announcement.MaximumTitleLength, notification.FindProperty(nameof(Notification.Title))?.GetMaxLength());
        Assert.Equal(Announcement.MaximumBodyLength, notification.FindProperty(nameof(Notification.Body))?.GetMaxLength());
        Assert.Equal(Announcement.MaximumBodyLength, announcement.FindProperty(nameof(Announcement.Body))?.GetMaxLength());
        Assert.Equal(
            new[]
            {
                nameof(Notification.Id),
                nameof(Notification.UserId),
                nameof(Notification.TargetAnnouncementId),
                nameof(Notification.TargetAnnouncementVersion),
            },
            notification.GetKeys().Single(key => !key.IsPrimaryKey()).Properties.Select(property => property.Name));
        Assert.Equal(
            new[]
            {
                nameof(AnnouncementTarget.AnnouncementId),
                nameof(AnnouncementTarget.UserId),
                nameof(AnnouncementTarget.AnnouncementVersion),
            },
            target.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.All(notification.GetForeignKeys(), foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        Assert.All(target.GetForeignKeys(), foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        IForeignKey targetProjection = Assert.Single(target.GetForeignKeys(), foreignKey =>
            foreignKey.GetConstraintName() == "fk_announcement_targets_notifications_projection");
        Assert.Equal(
            new[]
            {
                nameof(AnnouncementTarget.NotificationId),
                nameof(AnnouncementTarget.UserId),
                nameof(AnnouncementTarget.AnnouncementId),
                nameof(AnnouncementTarget.AnnouncementVersion),
            },
            targetProjection.Properties.Select(property => property.Name));
    }

    [Fact]
    public async Task RuntimeRoleCanUpdateOnlyConversationParticipantLeftAt()
    {
        await using var connection = new NpgsqlConnection(fixture.DatabaseConnection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using (var setRole = new NpgsqlCommand("SET ROLE dorosak_runtime", connection))
        {
            await setRole.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using (var allowed = new NpgsqlCommand(
                         "UPDATE communication.conversation_participants SET left_at = left_at WHERE false",
                         connection))
        {
            Assert.Equal(0, await allowed.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        }

        await using var denied = new NpgsqlCommand(
            "UPDATE communication.conversation_participants SET joined_at = joined_at WHERE false",
            connection);
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => denied.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        Assert.Equal("42501", exception.SqlState);
    }

    private static async Task<T> ExecuteScalarAsync<T>(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return Assert.IsType<T>(value);
    }
}
