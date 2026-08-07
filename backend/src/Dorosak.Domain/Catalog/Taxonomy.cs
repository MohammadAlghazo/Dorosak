namespace Dorosak.Domain.Catalog;

public sealed class Category
{
    private Category()
    {
    }

    private Category(Guid id, string code, Guid? parentId, int displayOrder, DateTimeOffset now)
    {
        Id = id;
        Code = code;
        ParentId = parentId;
        DisplayOrder = displayOrder;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public Guid? ParentId { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Category Create(string code, Guid? parentId, int displayOrder, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), code.Trim(), parentId, displayOrder, now);

    public static Category CreateSeeded(Guid id, string code, int displayOrder, DateTimeOffset now) =>
        new(id, code.Trim(), null, displayOrder, now);

    public void Update(Guid? parentId, int displayOrder, bool isActive, DateTimeOffset now)
    {
        ParentId = parentId;
        DisplayOrder = displayOrder;
        IsActive = isActive;
        UpdatedAt = now;
    }
}

public sealed class CategoryLocalization
{
    private CategoryLocalization()
    {
    }

    private CategoryLocalization(Guid categoryId, string locale, string name)
    {
        CategoryId = categoryId;
        Locale = locale;
        Name = name;
    }

    public Guid CategoryId { get; private set; }

    public string Locale { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public static CategoryLocalization Create(Guid categoryId, string locale, string name) =>
        new(categoryId, locale, name.Trim());

    public void Rename(string name) => Name = name.Trim();
}

public sealed class Tag
{
    private Tag()
    {
    }

    private Tag(Guid id, string code, DateTimeOffset now)
    {
        Id = id;
        Code = code;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Tag Create(string code, DateTimeOffset now) => new(Guid.CreateVersion7(), code.Trim(), now);

    public void SetActive(bool isActive, DateTimeOffset now)
    {
        IsActive = isActive;
        UpdatedAt = now;
    }
}

public sealed class TagLocalization
{
    private TagLocalization()
    {
    }

    private TagLocalization(Guid tagId, string locale, string name)
    {
        TagId = tagId;
        Locale = locale;
        Name = name;
    }

    public Guid TagId { get; private set; }

    public string Locale { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public static TagLocalization Create(Guid tagId, string locale, string name) => new(tagId, locale, name.Trim());

    public void Rename(string name) => Name = name.Trim();
}

public sealed class CourseCategory
{
    private CourseCategory()
    {
    }

    public CourseCategory(Guid courseId, Guid categoryId)
    {
        CourseId = courseId;
        CategoryId = categoryId;
    }

    public Guid CourseId { get; private set; }

    public Guid CategoryId { get; private set; }
}

public sealed class CourseTag
{
    private CourseTag()
    {
    }

    public CourseTag(Guid courseId, Guid tagId)
    {
        CourseId = courseId;
        TagId = tagId;
    }

    public Guid CourseId { get; private set; }

    public Guid TagId { get; private set; }
}
