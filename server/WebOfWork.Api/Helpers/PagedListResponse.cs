using System;
using System.Collections.Generic;

namespace WebOfWork.Api.Helpers;

/// <summary>
/// DRF-shaped paged envelope for endpoints that slice an already-materialised
/// list rather than paging in the database.
/// </summary>
/// <remarks>
/// Several list actions were returning <c>next = null, previous = null</c> (or
/// omitting the keys entirely) while the matching client page disabled its
/// paginator on a falsy value. That is a permanently disabled "Next": the user
/// can never leave page 1 no matter how many rows exist.
///
/// <see cref="PaginationLinks"/> is what fills the two link fields -- it needs
/// settable properties, so an anonymous type will not do.
/// <para>
/// <c>TotalPages</c> is computed rather than stored so it cannot disagree with
/// <c>Count</c> / <c>PageSize</c>.
/// </para>
/// </remarks>
/// <typeparam name="T">The row type.</typeparam>
public class PagedListResponse<T>
{
    /// <summary>The rows for the requested page.</summary>
    public IReadOnlyList<T> Results { get; set; } = Array.Empty<T>();

    /// <summary>Total rows matching the filter, across all pages.</summary>
    public int Count { get; set; }

    /// <summary>1-based page index actually returned.</summary>
    public int Page { get; set; }

    /// <summary>Rows per page after clamping.</summary>
    public int PageSize { get; set; }

    /// <summary>Derived, so it can never drift from Count/PageSize.</summary>
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Count / (double)PageSize);

    /// <summary>
    /// Absolute URL for the next page, or null on the last page.
    /// The client paginators gate on this being present.
    /// </summary>
    public string? Next { get; set; }

    /// <summary>Absolute URL for the previous page, or null on the first page.</summary>
    public string? Previous { get; set; }
}
