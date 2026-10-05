using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using WebOfWork.Api.Data;
using Xunit;

namespace WebOfWork.Api.Tests.Database;

/// <summary>
/// Verifies the <c>SeedSkillCatalog</c> migration against a real PostgreSQL
/// instance, in the two shapes a deployed database can actually be in.
///
/// ## Why this needs a database
///
/// The seed is written as raw SQL precisely because it has to be idempotent by
/// <i>name</i> rather than by id - ids differ between a populated database and an
/// empty one. That property cannot be checked by reading the SQL: only a real
/// database can tell us whether the guard clauses actually suppress the second
/// insert, and whether the legacy-name rename preserves the row id that
/// <c>FreelancerSkill</c> and <c>ProjectSkill</c> rows point at.
///
/// ## The two shapes
///
/// 1. <b>Empty</b> - a fresh clone running <c>dotnet ef database update</c>.
/// 2. <b>Legacy</b> - a database already seeded from <c>scripts/fixtures.sql</c>,
///    holding 'Development', 'Design', 'CSharp', 'React', 'Python', 'Figma'.
///
/// Both must converge on the same catalogue. If they do not, the same commit
/// produces two different databases depending on when it was checked out, and
/// the difference is invisible until a match silently stops scoring.
/// </summary>
[Trait("Category", "Container")]
public class SkillCatalogSeedTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("webofwork_catalog_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync() => await _dbContainer.StartAsync();

    public async Task DisposeAsync() => await _dbContainer.DisposeAsync();

    /// <summary>
    /// The catalogue the migration is expected to produce. Hard-coded rather
    /// than derived from the migration's own SQL: deriving it would make the
    /// assertion tautological, and the point is to pin the catalogue down.
    /// </summary>
    private static readonly Dictionary<string, string[]> Expected = new()
    {
        ["Development"] = new[]
        {
            "C#", "C++", "Java", "Python", "JavaScript", "TypeScript", "React",
            "Node.js", "ASP.NET Core", "PostgreSQL", "Docker", "Kubernetes",
            "Flutter", "React Native", "WordPress", "Shopify",
        },
        ["AI & Automation"] = new[]
        {
            "OpenAI", "Claude", "LangGraph", "RAG", "pgvector", "n8n", "MCP",
        },
        ["Design"] = new[]
        {
            "Figma", "Adobe Photoshop", "Logo Design", "Brand Identity",
            "UI Design", "UX Design", "Video Editing",
        },
        ["Marketing"] = new[] { "SEO", "Google Ads", "Email Marketing" },
        ["Data & Analytics"] = new[] { "Data Analysis", "Data Visualization" },
        ["Admin & Support"] = new[] { "Virtual Assistance", "Bookkeeping" },
        ["Writing & Translation"] = new[] { "Content Writing", "Translation" },
    };

    /// <summary>
    /// A fresh database name per call.
    ///
    /// Not an incrementing counter on the fixture: xUnit constructs a new test
    /// class instance per test method and runs the methods in parallel against
    /// one shared container, so an instance field would start at 1 in every
    /// test and all of them would race to create the same database. A GUID is
    /// unique per call regardless of which instance or thread it came from.
    /// </summary>
    private static string NewDatabaseName() => $"catalog_{Guid.NewGuid():N}";

    /// <summary>
    /// Creates a fresh empty database, brings it up to the migration immediately
    /// before the seed, applies whatever legacy rows the caller wants present,
    /// then runs the seed migration's own Up() on top.
    ///
    /// A new database per call rather than one shared scratch database: the
    /// convergence test needs two independently seeded databases side by side,
    /// and reusing one would mean the second call inherits the first call's rows
    /// and silently stops testing the "fresh" shape.
    /// </summary>
    private async Task<NpgsqlConnection> SeededConnectionAsync(
        params (string Name, string Skill)[] legacyRows)
    {
        var databaseName = NewDatabaseName();
        var builder = new NpgsqlConnectionStringBuilder(_dbContainer.GetConnectionString())
        {
            Database = databaseName,
        };

        await using (var admin = new NpgsqlConnection(
                         new NpgsqlConnectionStringBuilder(builder.ConnectionString)
                         {
                             Database = "postgres",
                         }.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand(
                $"CREATE DATABASE \"{databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = builder.ConnectionString;
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using (var context = new WebOfWorkDbContext(options))
        {
            // Migrate to the migration immediately before the seed, so the test
            // controls exactly which rows the seed finds already present.
            //
            // IMigrator rather than DatabaseFacade.MigrateAsync: the facade method
            // takes no target migration, so it would run everything including the
            // seed and leave nothing to control.
            var migrator = context.GetService<IMigrator>();
            var migrations = context.Database.GetMigrations().ToList();
            var seedIndex = migrations.FindIndex(m =>
                m.Contains("SeedSkillCatalog", StringComparison.Ordinal));
            seedIndex.Should().BeGreaterThanOrEqualTo(
                0, "the SeedSkillCatalog migration must exist for this test to mean anything");
            seedIndex.Should().BeGreaterThan(
                0, "the seed must come after the migration that creates the tables");

            await migrator.MigrateAsync(migrations[seedIndex - 1]);
        }

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();

            foreach (var (categoryName, skillName) in legacyRows)
            {
                // Matches what scripts/fixtures.sql does: resolve the category by
                // name, and only insert the skill when it is not already present.
                await using var insert = new NpgsqlCommand(
                    $"""
                     INSERT INTO categories (name)
                     SELECT '{categoryName}'
                     WHERE NOT EXISTS (SELECT 1 FROM categories WHERE name = '{categoryName}');

                     INSERT INTO skills (name, category_id, is_custom)
                     SELECT '{skillName}',
                            (SELECT id FROM categories WHERE name = '{categoryName}'),
                            false
                     WHERE NOT EXISTS (
                         SELECT 1 FROM skills s JOIN categories c ON c.id = s.category_id
                         WHERE s.name = '{skillName}' AND c.name = '{categoryName}');
                     """,
                    connection);
                await insert.ExecuteNonQueryAsync();
            }

            // Apply the seed migration only, via EF so the Up() under test is
            // exactly the committed one rather than a copy of its SQL.
            await using (var context = new WebOfWorkDbContext(options))
            {
                var seed = context.Database.GetMigrations()
                    .Single(m => m.Contains("SeedSkillCatalog", StringComparison.Ordinal));
                await context.GetService<IMigrator>().MigrateAsync(seed);
            }
        }

        // Returned already open: every caller issues queries on it directly, and
        // handing back a closed connection would make all of them fail on
        // "Connection is not open" rather than on anything to do with the seed.
        var result = new NpgsqlConnection(connectionString);
        await result.OpenAsync();
        return result;
    }

    /// <summary>
    /// Runs the seed migration's own statements a second time, straight from
    /// the migration file, without touching the migration history.
    ///
    /// Re-migrating would be the wrong way to test idempotence: EF records that
    /// the migration has run and would skip it entirely, so the test would
    /// assert nothing. Replaying the SQL is what "the guard clauses suppress the
    /// second insert" actually means, and it is also the real-world case -
    /// scripts/fixtures.sql and this migration can both have been applied.
    /// </summary>
    private static async Task ReplaySeedStatementsAsync(NpgsqlConnection connection)
    {
        var statements = ReadSeedStatements();
        foreach (var statement in statements)
        {
            await using var command = new NpgsqlCommand(statement, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Extracts every SQL statement from the committed SeedSkillCatalog
    /// migration. Read from the file rather than duplicated here, so the test
    /// cannot pass against a stale copy of the seed.
    /// </summary>
    private static IReadOnlyList<string> ReadSeedStatements()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "WebOfWork.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the solution file marks the repository root");
        var path = Path.Combine(
            directory!.FullName,
            "server", "WebOfWork.Api", "Data", "Migrations");

        var migration = Directory.GetFiles(path, "*SeedSkillCatalog.cs")
            .Single(f => !f.EndsWith(".Designer.cs", StringComparison.Ordinal));
        var source = File.ReadAllText(migration);

        var statements = new List<string>();
        foreach (System.Text.RegularExpressions.Match match in
                 System.Text.RegularExpressions.Regex.Matches(
                     source,
                     "migrationBuilder\\.Sql\\(\\s*\"(?<sql>(?:[^\"\\\\]|\\\\.)*)\"",
                     System.Text.RegularExpressions.RegexOptions.Singleline))
        {
            statements.Add(match.Groups["sql"].Value.Replace("\\\"", "\""));
        }

        statements.Should().NotBeEmpty($"the migration at {migration} must contain SQL");
        return statements;
    }

    private static async Task<List<(string Name, string Category, int Id)>> ReadCatalogueAsync(
        NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT s.name, c.name, s.id
            FROM skills s
            JOIN categories c ON c.id = s.category_id
            ORDER BY s.name, c.name
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(string, string, int)>();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));
        }

        return rows;
    }

    private static async Task<List<string>> ReadCategoriesAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT name FROM categories ORDER BY name", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    /// <summary>
    /// Runs a scalar-text query and returns one string per row, so an assertion
    /// about "no rows came back" reads as an empty list rather than as
    /// arithmetic on an ExecuteNonQuery result.
    /// </summary>
    private static async Task<List<string>> QueryStringsAsync(
        NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.IsDBNull(0) ? "<null>" : reader.GetString(0));
        }

        return values;
    }

    [Fact]
    public async Task Fresh_database_is_seeded_with_every_category()
    {
        await using var connection = await SeededConnectionAsync(Array.Empty<(string, string)>());

        var categories = await ReadCategoriesAsync(connection);

        categories.Should().BeEquivalentTo(Expected.Keys);
    }

    [Fact]
    public async Task Fresh_database_contains_the_representative_skills_of_each_category()
    {
        await using var connection = await SeededConnectionAsync(Array.Empty<(string, string)>());
        var catalogue = await ReadCatalogueAsync(connection);

        foreach (var (category, skills) in Expected)
        {
            var names = catalogue.Select(r => r.Name).ToList();
            foreach (var skill in skills)
            {
                names.Should().Contain(skill,
                    $"'{skill}' is a representative {category} skill and the catalogue is too thin without it");
            }
        }
    }

    [Fact]
    public async Task Seed_does_not_create_duplicate_skills_within_a_category()
    {
        await using var connection = await SeededConnectionAsync(Array.Empty<(string, string)>());

        var duplicates = await QueryStringsAsync(connection,
            "SELECT name || ' (category ' || category_id || ')' "
            + "FROM skills GROUP BY name, category_id HAVING count(*) > 1");

        duplicates.Should().BeEmpty(
            "one skill means one row; two rows for the same name in the same "
            + "category is what makes a catalogue look like it has "
            + "several types of the same skill");
    }

    [Fact]
    public async Task Seed_is_idempotent_when_replayed_against_an_already_seeded_database()
    {
        await using var connection = await SeededConnectionAsync(Array.Empty<(string, string)>());

        var catalogueBefore = await ReadCatalogueAsync(connection);
        var categoriesBefore = await ReadCategoriesAsync(connection);

        await ReplaySeedStatementsAsync(connection);

        var catalogueAfter = await ReadCatalogueAsync(connection);
        var categoriesAfter = await ReadCategoriesAsync(connection);

        categoriesAfter.Should().BeEquivalentTo(categoriesBefore,
            "a second application must not create a second category of the same name");
        catalogueAfter.Should().BeEquivalentTo(catalogueBefore,
            "a second application must not duplicate or rename any skill");
    }

    [Fact]
    public async Task Replaying_the_seed_does_not_disturb_a_legacy_name_that_is_already_canonical()
    {
        await using var connection = await SeededConnectionAsync(("Development", "CSharp"));

        var before = await ReadCatalogueAsync(connection);
        await ReplaySeedStatementsAsync(connection);
        var after = await ReadCatalogueAsync(connection);

        after.Should().BeEquivalentTo(before,
            "once 'CSharp' has been folded to 'C#', replaying must find nothing left "
            + "to do - in particular it must not try to rename 'C#' onto itself or "
            + "insert a competing row");
    }

    [Fact]
    public async Task Legacy_skill_names_are_folded_into_their_canonical_form()
    {
        // 'CSharp' is what scripts/fixtures.sql seeds; the catalogue calls it 'C#'.
        await using var connection = await SeededConnectionAsync(("Development", "CSharp"));

        var catalogue = await ReadCatalogueAsync(connection);

        catalogue.Should().NotContain(r => r.Name == "CSharp",
            "'CSharp' and 'C#' are one skill, and two rows for one skill means a "
            + "profile tagged 'CSharp' cannot match a project requiring 'C#'");
        catalogue.Should().Contain(r => r.Name == "C#");
    }

    [Fact]
    public async Task Folding_a_legacy_name_preserves_the_row_id_that_profiles_point_at()
    {
        await using var connection = await SeededConnectionAsync(("Development", "CSharp"));

        // Read the id first, close the reader, then query by it. A Npgsql
        // connection runs one command at a time, so nesting the second query
        // inside the first reader's scope would fail with "A command is already
        // in progress" rather than testing anything.
        var ids = await QueryStringsAsync(connection,
            "SELECT id::text FROM skills WHERE name IN ('CSharp', 'C#') ORDER BY id");

        ids.Should().HaveCount(1,
            "'CSharp' and 'C#' are one skill, so exactly one row must remain - the "
            + "migration renames in place rather than inserting a replacement");
        var survivingId = ids[0];

        // A profile tagged 'CSharp' before the migration still points at this id.
        // Renaming in place is what keeps it attached; inserting a new row and
        // deleting the old one would leave every such profile dangling.
        var names = await QueryStringsAsync(connection,
            $"SELECT name FROM skills WHERE id = {survivingId}");

        names.Should().ContainSingle(
            "renaming in place keeps one row, so the id profiles point at resolves "
            + "to exactly one skill");
        names[0].Should().Be("C#",
            "the row that profiles point at must now carry the canonical name");
    }

    [Fact]
    public async Task A_database_that_already_has_the_catalogue_converges_with_a_fresh_one()
    {
        await using var fresh = await SeededConnectionAsync(Array.Empty<(string, string)>());
        var freshCatalogue = await ReadCatalogueAsync(fresh);
        var freshCategories = await ReadCategoriesAsync(fresh);

        // The legacy shape scripts/fixtures.sql produces, verbatim.
        await using var legacy = await SeededConnectionAsync(
            ("Development", "CSharp"),
            ("Development", "React"),
            ("Development", "Python"),
            ("Design", "Figma"));
        var legacyCatalogue = await ReadCatalogueAsync(legacy);
        var legacyCategories = await ReadCategoriesAsync(legacy);

        legacyCategories.Should().BeEquivalentTo(freshCategories,
            "the same commit must not produce two different catalogues");
        legacyCatalogue.Select(r => (r.Name, r.Category))
            .Should().BeEquivalentTo(freshCatalogue.Select(r => (r.Name, r.Category)),
                "skill identity is (name, category); ids may legitimately differ");
    }

    [Fact]
    public async Task Seeded_skills_are_not_marked_custom()
    {
        await using var connection = await SeededConnectionAsync(Array.Empty<(string, string)>());

        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM skills WHERE is_custom", connection);
        var custom = (long)(await command.ExecuteScalarAsync())!;

        custom.Should().Be(0,
            "catalogue skills are system-provided; IsCustom is for skills a user "
            + "invents that the catalogue does not have");
    }
}
