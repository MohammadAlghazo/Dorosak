namespace Dorosak.Domain.Authoring;

public sealed class CourseSection
{
    private CourseSection()
    {
    }

    private CourseSection(Guid id, Guid draftId, int position, DateTimeOffset now)
    {
        Id = id;
        DraftId = draftId;
        Position = position;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid DraftId { get; private set; }

    public Guid? CurrentRevisionId { get; private set; }

    public int Position { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public static CourseSection Create(Guid id, Guid draftId, int position, DateTimeOffset now) =>
        new(id == Guid.Empty ? Guid.CreateVersion7() : id, draftId, position, now);

    public void ApplyRevision(Guid revisionId, int position)
    {
        CurrentRevisionId = revisionId;
        Position = position;
        RemovedAt = null;
    }

    public void Remove(DateTimeOffset now) => RemovedAt = now;
}

public sealed class SectionRevision
{
    private SectionRevision()
    {
    }

    private SectionRevision(Guid id, Guid sectionId, long draftVersion, string title, int position, DateTimeOffset now)
    {
        Id = id;
        SectionId = sectionId;
        DraftVersion = draftVersion;
        Title = title;
        Position = position;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid SectionId { get; private set; }

    public long DraftVersion { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public int Position { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static SectionRevision Create(
        Guid sectionId,
        long draftVersion,
        string title,
        int position,
        DateTimeOffset now) => new(Guid.CreateVersion7(), sectionId, draftVersion, title.Trim(), position, now);
}

public sealed class CourseLesson
{
    private CourseLesson()
    {
    }

    private CourseLesson(Guid id, Guid draftId, Guid sectionId, int position, DateTimeOffset now)
    {
        Id = id;
        DraftId = draftId;
        SectionId = sectionId;
        Position = position;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid DraftId { get; private set; }

    public Guid SectionId { get; private set; }

    public Guid? CurrentRevisionId { get; private set; }

    public int Position { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public static CourseLesson Create(Guid id, Guid draftId, Guid sectionId, int position, DateTimeOffset now) =>
        new(id == Guid.Empty ? Guid.CreateVersion7() : id, draftId, sectionId, position, now);

    public void ApplyRevision(Guid sectionId, Guid revisionId, int position)
    {
        SectionId = sectionId;
        CurrentRevisionId = revisionId;
        Position = position;
        RemovedAt = null;
    }

    public void Remove(DateTimeOffset now) => RemovedAt = now;
}

public sealed class LessonRevision
{
    private LessonRevision()
    {
    }

    private LessonRevision(
        Guid id,
        Guid lessonId,
        long draftVersion,
        string title,
        string lessonType,
        string content,
        int position,
        DateTimeOffset now,
        Guid? mediaAssetId)
    {
        Id = id;
        LessonId = lessonId;
        DraftVersion = draftVersion;
        Title = title;
        LessonType = lessonType;
        Content = content;
        MediaAssetId = mediaAssetId;
        Position = position;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid LessonId { get; private set; }

    public long DraftVersion { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string LessonType { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public Guid? MediaAssetId { get; private set; }

    public int Position { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static LessonRevision Create(
        Guid lessonId,
        long draftVersion,
        string title,
        string lessonType,
        string content,
        int position,
        DateTimeOffset now,
        Guid? mediaAssetId = null) => new(
            Guid.CreateVersion7(),
            lessonId,
            draftVersion,
            title.Trim(),
            lessonType,
            content.Trim(),
            position,
            now,
            mediaAssetId);
}
