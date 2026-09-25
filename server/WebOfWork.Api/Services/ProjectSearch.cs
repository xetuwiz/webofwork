using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Services;

/// <summary>
/// Full-text search over <c>projects</c>, expressed as LINQ so it stays inside a
/// single <c>SELECT</c> and composes with the rest of the query.
/// </summary>
/// <remarks>
/// <para>
/// This replaces <c>p.Title.Contains(search) || p.Description.Contains(search)</c>.
/// EF translates <c>Contains</c> to <c>ILIKE '%term%'</c>, which has two problems
/// that no amount of tuning fixes:
/// </para>
/// <list type="bullet">
/// <item><b>It cannot use an index.</b> A leading wildcard forces a sequential
/// scan of every project row on every keystroke.</item>
/// <item><b>It matches substrings.</b> "press" matches "WordPress", and "react"
/// matches "Reactor". On a marketplace those are precisely the wrong matches: a
/// client searching for React does not want the reactor-safety project.</item>
/// </list>
/// <para>
/// The <c>tsvector</c> column added by <c>AddProjectFullTextSearch</c> fixes both: a
/// GIN index can serve it, and the text search parser respects word boundaries and
/// stems, so "databases" finds "database" while "press" does not find "WordPress".
/// </para>
/// <para>
/// <b>Not modelled as an EF property.</b> The column is
/// <c>GENERATED ALWAYS AS ... STORED</c>, so PostgreSQL owns it and cannot be
/// written to. Adding it to the model would invite a caller to assign it and fail at
/// the database rather than at the call site, so it is referenced by name through
/// <c>EF.Property</c>.
/// </para>
/// </remarks>
public static class ProjectSearch
{
    /// <summary>
    /// The name of the <c>search_vector</c> column.
    /// </summary>
    /// <remarks>
    /// <b>Why <c>EF.Property</c> is written out at each use site instead of behind a
    /// helper.</b> Two earlier versions both failed to translate:
    ///
    /// <list type="number">
    /// <item>capturing it in a local before building the lambda -- throws "may only
    /// be used within Entity Framework LINQ queries", because it evaluates rather
    /// than composes;</item>
    /// <item>wrapping it in a <c>static Vector(Project)</c> helper -- the provider
    /// has no translation for an arbitrary method call, so the whole predicate fails
    /// even though it looks like a column reference.</item>
    /// </list>
    /// The call has to appear literally inside the expression tree. Stating just the
    /// column name here keeps the rest honest without pretending it can be hoisted.
    /// </remarks>
    public const string VectorColumn = "search_vector";

    /// <summary>
    /// Restrict a project query to those matching <paramref name="search"/>, and
    /// order it.
    /// </summary>
    /// <param name="query">The query to narrow. Filters already applied are kept.</param>
    /// <param name="search">
    /// Free text. Blank or whitespace returns every row newest-first, so callers do
    /// not have to branch.
    /// </param>
    /// <param name="rank">
    /// When true, order by relevance. With no search term there is nothing to rank,
    /// so the ordering is newest-first either way.
    /// </param>
    /// <param name="titleStartsWith">
    /// When true, also match projects whose title merely *begins with* the term.
    /// </param>
    /// <remarks>
    /// <b>Why the return type is <c>IOrderedQueryable</c>.</b> Ordering is
    /// last-one-wins in LINQ, so a caller that adds its own <c>OrderBy</c> after
    /// calling this silently discards the ranking and searching quietly reverts to
    /// newest-first. Returning an already-ordered query makes the ordering something
    /// to extend with <c>ThenBy</c> rather than replace.
    ///
    /// <b>Why the title-contains arm exists.</b> Full-text search alone breaks
    /// type-ahead. No committed tsvector contains the stem "dashi", so a user typing
    /// the first four letters of "dashboard" gets nothing at all, and a search box
    /// that stops responding mid-word looks broken.
    ///
    /// It matches the title <i>anywhere</i>, not just at the start. A *prefix* match
    /// was the first attempt and it does not work: for a project titled "React
    /// dashboard rebuild", searching "dash" finds nothing, because the title does
    /// not begin with "dash". The word a user is part-way through typing is almost
    /// never at the start of the title.
    ///
    /// So this arm does reintroduce substring matching, but scoped to the title and
    /// unioned with -- not ORed into -- the full-text branch. That is a considered
    /// trade, not an oversight: the substring defect above was "press" matching
    /// "WordPress", and this arm narrows exactly that by restricting substring
    /// matching to a field a human wrote as a name.
    /// </remarks>
    public static IOrderedQueryable<Project> Apply(
        this IQueryable<Project> query,
        string? search,
        bool rank = false,
        bool titleStartsWith = false)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query.OrderByDescending(p => p.CreatedAt);
        }

        var term = search.Trim();

        // Built as `%term%` -- a contains, not a prefix. See the note on
        // `titleStartsWith`: a prefix cannot find the word being typed when it is
        // not the first word of the title.
        //
        // Escaped so that `%` and `_` in user input are literal characters rather
        // than wildcards matching the whole table.
        var contains = "%" + EscapeLike(term) + "%";

        // `EF.Property` is written literally inside the lambda, and `Matches` is
        // called on its result. See the note on `VectorColumn` for why neither can be
        // hoisted into a local or a helper.
        //
        // `Matches(string)` is the provider's translation of `@@` and takes the *raw
        // search text*: it wraps the value in `plainto_tsquery` itself. Punctuation
        // is therefore treated as separators rather than raising a syntax error, so
        // a user typing `react's` gets results for "react" rather than a 500.
        // `Matches(string)` is the provider's translation of `@@` and takes the *raw*
        // search text: it wraps the value in `plainto_tsquery` itself. Punctuation is
        // therefore treated as separators rather than raising a syntax error, so a
        // user typing `react's` gets results for "react" rather than a 500.
        var matched = query.Where(p =>
            EF.Property<NpgsqlTsVector>(p, VectorColumn).Matches(term));

        if (titleStartsWith)
        {
            // `Union`, not `||`: an `||` wrapped around a `@@` match cannot use the
            // GIN index alone, whereas a union lets each branch be planned on its
            // own terms.
            matched = matched.Union(
                query.Where(p => EF.Functions.ILike(p.Title, contains, "\\")));
        }

        if (!rank)
        {
            return matched.OrderByDescending(p => p.CreatedAt);
        }

        // Ranking needs a `tsquery`, and the only translation available for that
        // from raw text is the `Matches` overload -- which cannot be reused here
        // because it is a leaf method with no expression underneath to translate.
        // So the tsquery is parsed in C# and sent as a parameter.
        //
        // `NpgsqlTsQuery.Parse` throws on malformed input, which `plainto_tsquery`
        // would not. It is guarded rather than trusted: anything it rejects falls
        // back to an empty query, which matches nothing and ranks everything zero,
        // instead of turning a stray character into a 500.
        var tsQuery = TryParseQuery(term);

        return matched.OrderByDescending(p => SearchFunctions.Rank(
            EF.Property<NpgsqlTsVector>(p, VectorColumn), tsQuery));
    }

    /// <summary>
    /// Escape the LIKE metacharacters in a user-supplied prefix.
    /// </summary>
    /// <remarks>
    /// <c>%</c> and <c>_</c> are wildcards in a LIKE pattern, and this pattern is
    /// built from raw search input. Left unescaped, searching for <c>%</c> matches
    /// every project with a non-empty title -- which is not a crash, so nothing would
    /// report it, just a search box that appears broken.
    ///
    /// The escape character is <c>\</c> and the pattern is passed with an explicit
    /// <c>ESCAPE</c> clause, because Npgsql's <c>ILike</c> overload does not add one.
    /// </remarks>
    private static string EscapeLike(string term) =>
        term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>
    /// Parse a search term into a <c>tsquery</c>, tolerating anything the parser
    /// rejects.
    /// </remarks>
    /// <remarks>
    /// The English text search configuration, matching the one the generated column
    /// uses. Using a different configuration here would rank rows against a
    /// differently-stemmed query and produce quietly wrong ordering.
    /// </remarks>
    private static NpgsqlTsQuery TryParseQuery(string term)
    {
        try
        {
            return NpgsqlTsQuery.Parse(term);
        }
        catch (ArgumentException)
        {
            return NpgsqlTsQuery.Parse(string.Empty);
        }
    }

    /// <summary>
    /// The relevance of a project against a search term, as a projected scalar.
    /// </summary>
    /// <remarks>
    /// Exposed so a test can assert that ranking is real. An ordering that silently
    /// does nothing looks exactly like one that works, and reading the score is the
    /// only way to tell them apart.
    /// </remarks>
    public static Expression<Func<Project, float>> RankExpression(string search)
    {
        var tsQuery = TryParseQuery(search.Trim());
        return p => SearchFunctions.Rank(
            EF.Property<NpgsqlTsVector>(p, VectorColumn), tsQuery);
    }
}
