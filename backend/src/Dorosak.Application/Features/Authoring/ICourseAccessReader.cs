namespace Dorosak.Application.Features.Authoring;

public interface ICourseAccessReader
{
    Task<bool> CanAccessAsync(Guid courseId, Guid userId, CourseAccess access, CancellationToken cancellationToken);
}
