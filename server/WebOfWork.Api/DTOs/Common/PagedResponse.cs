using System;
using System.Collections.Generic;

namespace WebOfWork.Api.DTOs.Common;

public class PagedResponse<T>
{
    public IReadOnlyList<T> Results { get; set; } = Array.Empty<T>();
    public int Count { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Count / (double)PageSize);
}
