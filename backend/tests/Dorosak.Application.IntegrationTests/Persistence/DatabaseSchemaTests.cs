using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
        Assert.Equal(10L, await ExecuteScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'assessment' AND table_name IN ('quizzes', 'quiz_versions', 'questions', 'question_options', 'quiz_attempts', 'quiz_answers', 'assignments', 'assignment_versions', 'assignment_submissions', 'grade_revisions')",
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
        Assert.Equal("20260806063112_AddSchemaCompatibilityMarker", boundaries[0]);
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

        string pendingIndex = await ExecuteScalarAsync<string>(
            connection,
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'operations' AND indexname = 'ix_outbox_messages_pending'",
            TestContext.Current.CancellationToken);
        Assert.Contains("processed_at IS NULL", pendingIndex, StringComparison.Ordinal);
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
