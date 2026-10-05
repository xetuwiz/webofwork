using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WebOfWork.Api.DTOs.Common;

public class PagedResponse<T>
{
    public IReadOnlyList<T> Results { get; set; } = Array.Empty<T>();
    public int Count { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Count / (double)PageSize);
    /// <summary>
    /// DRF PageNumberPagination always emits next/previous (absolute URL, or null at the boundary).
    /// The frontend gates its paginator buttons on these, so they must be present even when null.
    /// </summary>
    public string? Next { get; set; }
    public string? Previous { get; set; }

    /// <summary>
    /// Per-status row counts for a tabbed list, e.g. { all: 12, pending: 3, ... }.
    /// </summary>
    /// <remarks>
    /// Written only when a caller sets it, so the ~30 endpoints that do not use
    /// it keep their existing payload shape. A tabbed list needs the counts for
    /// every tab, not just the active one, so they cannot be derived from
    /// `Count` (which reflects the status filter that produced this page).
    ///
    /// This was previously read by the client as `total_counts` and hardcoded
    /// to 0 when absent, which made every tab label read "(0)" while the rows
    /// rendered normally -- the counts looked like a client bug but the field was
    /// never sent.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, int>? TotalCounts { get; set; }
}
