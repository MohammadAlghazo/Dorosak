using Dorosak.Application.Common.Persistence;
using Dorosak.Domain.Identity;
using Dorosak.Domain.Authoring;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Operations;
using Dorosak.Domain.Profiles;
using Dorosak.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Persistence;

public sealed class DorosakDbContext(DbContextOptions<DorosakDbContext> options)
    : IdentityDbContext<
        ApplicationUser,
        ApplicationRole,
        Guid,
        IdentityUserClaim<Guid>,
        IdentityUserRole<Guid>,
        IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>,
        IdentityUserToken<Guid>>(options), IUnitOfWork, IDataProtectionKeyContext
{
    public const string DefaultSchema = "app";

    public const string MigrationsSchema = "migrations";

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    internal DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    internal DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    internal DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    internal DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();

    internal DbSet<MfaChallenge> MfaChallenges => Set<MfaChallenge>();

    internal DbSet<MfaRecoveryCode> MfaRecoveryCodes => Set<MfaRecoveryCode>();

    internal DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    internal DbSet<TeacherApplication> TeacherApplications => Set<TeacherApplication>();

    internal DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();

    internal DbSet<Course> Courses => Set<Course>();

    internal DbSet<CourseLocalization> CourseLocalizations => Set<CourseLocalization>();

    internal DbSet<CourseSlug> CourseSlugs => Set<CourseSlug>();

    internal DbSet<CourseInstructor> CourseInstructors => Set<CourseInstructor>();

    internal DbSet<Category> Categories => Set<Category>();

    internal DbSet<CategoryLocalization> CategoryLocalizations => Set<CategoryLocalization>();

    internal DbSet<Tag> Tags => Set<Tag>();

    internal DbSet<TagLocalization> TagLocalizations => Set<TagLocalization>();

    internal DbSet<CourseCategory> CourseCategories => Set<CourseCategory>();

    internal DbSet<CourseTag> CourseTags => Set<CourseTag>();

    internal DbSet<CourseDraft> CourseDrafts => Set<CourseDraft>();

    internal DbSet<CourseSection> CourseSections => Set<CourseSection>();

    internal DbSet<SectionRevision> SectionRevisions => Set<SectionRevision>();

    internal DbSet<CourseLesson> CourseLessons => Set<CourseLesson>();

    internal DbSet<LessonRevision> LessonRevisions => Set<LessonRevision>();

    internal DbSet<PublicationReview> PublicationReviews => Set<PublicationReview>();

    internal DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Ignore<IdentityUserPasskey<Guid>>();
        builder.HasDefaultSchema(DefaultSchema);
        builder.HasPostgresExtension("pg_trgm");
        builder.HasPostgresExtension("unaccent");
        builder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);
    }

    public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            TResponse response = await operation(cancellationToken);
            await SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        });
    }
}
