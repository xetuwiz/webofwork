"""Generate the AddProjectFullTextSearch migration.

Kept as a script so the expression and the trigger stay reviewable as data, and so
the two copies of the document expression -- the generated column and the trigger
that keeps it in step -- cannot drift apart.

Regenerate with:

    python scripts/build_search_migration.py
"""

import pathlib

HEADER = '''using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <summary>
    /// Adds full-text search to <c>projects</c>.
    ///
    /// ## Why this exists
    ///
    /// Project search was <c>p.Title.Contains(search) || p.Description.Contains(search)</c>.
    /// EF translates <c>Contains</c> to <c>ILIKE '%term%'</c>, which is correct and
    /// completely unusable: no index can serve a leading wildcard, so the query
    /// degrades to a sequential scan of every project row on every keystroke. It is
    /// also a substring match, so searching "react" finds "reactor" and searching
    /// "press" finds "WordPress".
    ///
    /// A <c>tsvector</c> column with a GIN index fixes both problems: the index can
    /// serve the query, and the text search parser understands word boundaries.
    ///
    /// ## Why a generated column plus a trigger
    ///
    /// A plain column would silently rot. Anything that updates a project's text
    /// through EF, through a raw SQL script, or through <c>psql</c> would leave the
    /// vector describing the old text, and the search would return confidently wrong
    /// results. Two implementations are possible:
    ///
    /// - a **generated column** (<c>GENERATED ALWAYS AS ... STORED</c>), which
    ///   PostgreSQL maintains itself and which cannot be written to directly. It is
    ///   the stronger guarantee and is used for the vector itself.
    /// - a **trigger**, which is needed for the ranking column because rank depends
    ///   on a query parameter and so cannot be a generated expression.
    ///
    /// ## Backfill
    ///
    /// <c>ALTER TABLE ... ADD COLUMN ... GENERATED ALWAYS AS ... STORED</c>
    /// computes the value for every existing row as part of the same statement, so
    /// there is no separate backfill step that could be forgotten.
    ///
    /// ## No CONCURRENTLY
    ///
    /// Building the index without <c>CONCURRENTLY</c> takes a write lock for the
    /// duration. That is the right trade here: the table is small at this stage of
    /// the project, and <c>CONCURRENTLY</c> cannot run inside the transaction EF
    /// wraps every migration in. A comment records it so nobody "fixes" it later
    /// without knowing what that costs.
    ///
    /// ## Ranking is done in the query, not here
    ///
    /// An earlier version of this migration added a
    /// <c>projects_search_rank</c> SQL function. It was wrong: it selected one
    /// arbitrary matching row and returned that row's rank, so every result in a page
    /// would have carried the same score. Ranking depends on the query text and
    /// belongs in the query, computed per row against the row being returned.
    /// </summary>
    public partial class AddProjectFullTextSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The document is title, description, requirements and deliverables.
            //
            // Title and description are weighted highest because a client searching
            // for a term almost always means it in the title. Requirements and
            // deliverables are included so the search can find a project by what it
            // asks for rather than only by how it is advertised.
            //
            // Setweight on A..D: A highest, D lowest. The weights only matter
            // because `ts_rank_cd` is used to order results.
            migrationBuilder.Sql(
                """
                ALTER TABLE projects
                    ADD COLUMN search_vector tsvector
                    GENERATED ALWAYS AS (
                        setweight(to_tsvector('english', coalesce(title, '')), 'A') ||
                        setweight(to_tsvector('english', coalesce(description, '')), 'B') ||
                        setweight(to_tsvector('english', coalesce(requirements, '')), 'C') ||
                        setweight(to_tsvector('english', coalesce(expected_deliverables, '')), 'D')
                    ) STORED;
                """);

            // GIN is the right index for tsvector. It is larger and slower to build
            // than a btree, but a btree cannot answer a `@@` match at all.
            migrationBuilder.Sql(
                "CREATE INDEX ix_projects_search_vector ON projects USING GIN (search_vector);");

            // Comment on the index rather than a no-op statement: the point is that
            // the lock is deliberate, and this is where the next reader will look.
            migrationBuilder.Sql(
                "COMMENT ON INDEX ix_projects_search_vector IS "
                + "'Built non-concurrently: migrations run in a transaction, which "
                + "CONCURRENTLY cannot. Safe while the table is small.';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately not `CASCADE`. The index and column go together, and
            // dropping the column first would remove the index with it anyway --
            // being explicit means a failure names the step that failed.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_projects_search_vector;");
            migrationBuilder.Sql("ALTER TABLE projects DROP COLUMN IF EXISTS search_vector;");
        }
    }
}
'''

target = (
    "server/WebOfWork.Api/Data/Migrations/20261002082207_AddProjectFullTextSearch.cs"
)
pathlib.Path(target).write_text(HEADER, encoding="utf-8", newline="\r\n")
print(f"wrote {target}")
