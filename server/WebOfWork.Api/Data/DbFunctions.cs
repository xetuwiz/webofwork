using System;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace WebOfWork.Api.Data;

/// <summary>
/// Server-side functions EF does not translate on its own, mapped so they can be
/// used inside a LINQ <c>WHERE</c> or <c>ORDER BY</c>.
/// </summary>
/// <remarks>
/// <para>
/// EF's Npgsql provider exposes <c>@@</c> (via <c>NpgsqlTsVector.Matches</c>) but
/// has no <c>ts_rank_cd</c>. Ranking therefore has no LINQ form and would otherwise
/// force the query to <c>ToListAsync</c> and sort in C#, which pages badly and
/// fetches every matching row.
/// </para>
/// <para>
/// Mapping the function to its SQL name keeps the whole thing in one
/// <c>SELECT</c>, where PostgreSQL can use the GIN index for the filter and rank
/// only the rows it returns.
/// </para>
/// <para>
/// The methods must be static, and must not touch anything but their arguments:
/// EF compiles them into the query pipeline, so a body that read managed state
/// would either be ignored or throw.
/// </para>
/// </remarks>
public static class SearchFunctions
{
    /// <summary>
    /// PostgreSQL <c>ts_rank_cd(tsvector, tsquery)</c> -- cover-density ranking.
    /// </summary>
    /// <param name="vector">The stored search vector for the row.</param>
    /// <param name="query">A <c>tsquery</c>, i.e. <c>NpgsqlTsQuery</c> not a string.</param>
    /// <remarks>
    /// <para>
    /// The parameter type matters: PostgreSQL has no <c>ts_rank_cd(tsvector, text)</c>
    /// overload, only <c>ts_rank_cd(tsvector, tsquery)</c>. An earlier version took a
    /// <c>string</c> and every ranked query failed with
    /// <c>42883: function ts_rank_cd(tsvector, text) does not exist</c> -- at
    /// execution time, with no compile-time warning that the signature was wrong.
    /// </para>
    /// <para>
    /// <c>cd</c> rather than the default ranking because it rewards terms appearing
    /// close together and in order, which is what "dashboard" matching a title
    /// <c>Analytics dashboard</c> should score higher than one matching a
    /// requirements paragraph that happens to contain both words.
    /// </para>
    /// <para>
    /// The body throws on purpose: EF replaces it with the SQL call, and a body that
    /// returned a plausible number would be a trap for anyone who later calls it
    /// outside a query, where it would silently return a fabricated rank.
    /// </para>
    /// </remarks>
    public static float Rank(NpgsqlTsVector vector, NpgsqlTsQuery query) =>
        throw new NotSupportedException(
            "Replaced by the SQL translation of ts_rank_cd; this body is never run.");
}
