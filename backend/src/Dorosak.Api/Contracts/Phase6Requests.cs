using Dorosak.Application.Features.Profiles.TeacherApplications;
using Dorosak.Application.Features.Authoring;
using Dorosak.Application.Features.PublishingCoordinator;
using Dorosak.Application.Features.Catalog;

namespace Dorosak.Api.Contracts;

public sealed record TeacherApplicationRequest(
    string Headline,
    string Biography,
    string Expertise,
    string Motivation);

public sealed record TeacherApplicationReviewRequest(string Decision, string? Reason);

public sealed record CourseCreateRequest(
    string DefaultLocale,
    string Level,
    IReadOnlyList<CourseLocalizationInput> Localizations,
    IReadOnlyList<string>? CategoryCodes,
    IReadOnlyList<string>? TagCodes);

public sealed record CourseMetadataRequest(
    string DefaultLocale,
    string Level,
    IReadOnlyList<CourseLocalizationInput> Localizations,
    IReadOnlyList<string>? CategoryCodes,
    IReadOnlyList<string>? TagCodes);

public sealed record CurriculumUpdateRequest(IReadOnlyList<SectionInput> Sections);

public sealed record CollaboratorRequest(Guid UserId, string Role);

public sealed record TransferCourseOwnershipRequest(Guid NewOwnerUserId);

public sealed record ArchiveCourseRequest(string Reason);

public sealed record PublicationReviewRequest(string Decision, string? Reason);

public sealed record CategoryUpsertRequest(
    string Code,
    Guid? ParentId,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyList<TaxonomyLocalizationInput> Localizations);

public sealed record TagUpsertRequest(
    string Code,
    bool IsActive,
    IReadOnlyList<TaxonomyLocalizationInput> Localizations);

