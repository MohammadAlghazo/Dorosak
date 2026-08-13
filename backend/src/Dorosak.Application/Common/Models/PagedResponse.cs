namespace Dorosak.Application.Common.Models;

public sealed record PagedResponse<T>(System.Collections.Generic.IReadOnlyList<T> Items, string? NextCursor, bool HasMore);
