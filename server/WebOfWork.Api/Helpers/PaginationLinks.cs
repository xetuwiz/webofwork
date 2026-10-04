using Microsoft.AspNetCore.Http;

namespace WebOfWork.Api.Helpers;

/// <summary>
/// Builds DRF-style <c>next</c>/<c>previous</c> links so paginated responses match the reference,
/// which always includes those keys (absolute URL, or null at the first/last page).
/// The frontend disables its paginator buttons on falsy values, so a missing key means
/// a permanently disabled "next".
/// </summary>
public static class PaginationLinks
{
    /// <summary>
    /// Populates the next/previous setters for the given page, keeping every other
    /// query string parameter intact.
    /// </summary>
    public static void Apply(
        HttpRequest request,
        int page,
        int pageSize,
        int count,
        Action<string?> setNext,
        Action<string?> setPrevious)
    {
        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(count / (double)pageSize);
        setNext(page < totalPages ? Build(request, page + 1) : null);
        setPrevious(page > 1 ? Build(request, page - 1) : null);
    }

    private static string Build(HttpRequest request, int page)
    {
        var query = new List<KeyValuePair<string, string?>>();
        foreach (var pair in request.Query)
        {
            if (string.Equals(pair.Key, "page", StringComparison.OrdinalIgnoreCase)) continue;
            query.Add(new KeyValuePair<string, string?>(pair.Key, pair.Value));
        }
        query.Add(new KeyValuePair<string, string?>("page", page.ToString()));

        // Preserve PathBase so the same helper works for aliased routes too.
        var pathBase = request.PathBase.HasValue ? request.PathBase.Value! : string.Empty;
        var queryString = QueryString.Create(query);
        return $"{request.Scheme}://{request.Host}{pathBase}{request.Path}{queryString}";
    }
}
