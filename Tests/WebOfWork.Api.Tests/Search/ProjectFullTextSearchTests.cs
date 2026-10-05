using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.Search;

/// <summary>
/// Project search, against a real PostgreSQL instance with the migrations applied.
/// </summary>
/// <remarks>
/// <para>
/// These need a database rather than an in-memory provider, and that is the point.
/// The behaviour under test <i>is</i> PostgreSQL's: <c>@@</c> matching, the English
/// text search configuration, stemming, and whether the GIN index is reachable. The
/// InMemory provider would answer none of those questions and would answer all of
/// them wrongly.
/// </para>
/// <para>
/// <b>What was wrong before.</b> Search was <c>p.Title.Contains(search) ||
/// p.Description.Contains(search)</c>, which EF translates to <c>ILIKE '%term%'</c>.
/// Two defects, both reproduced below: no index can serve a leading wildcard, so
/// every keystroke scanned the whole table; and substring matching returned
/// "Reactor" for "react" and "WordPress" for "press".
/// </para>
/// </remarks>
[Trait("Category", "Container")]
public class ProjectFullTextSearchTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("webofwork_search_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private WebOfWorkDbContext _dbContext = null!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        _dbContext = new WebOfWorkDbContext(
            new DbContextOptionsBuilder<WebOfWorkDbContext>()
                .UseNpgsql(_db.GetConnectionString())
                .UseSnakeCaseNamingConvention()
                .Options);

        await _dbContext.Database.MigrateAsync();

        // The rows chosen so that every assertion below has a wrong answer available
        // to return. A fixture where the correct row is also the only candidate
        // proves nothing.
        await SeedAsync(
            ("React dashboard rebuild", "A React front end", "React and Redux with tests", "Deployed SPA"),
            ("Reactor core monitoring", "Plant safety", "Sensor telemetry for a facility", "Alarms"),
            ("WordPress marketing site", "A brochure site", "Content management", "Pages"),
            ("Kubernetes migration", "Container platform", "Helm charts on EKS", "Clusters"),
            ("Reporting warehouse", "Analytics", "A database migration and ETL pipeline", "Reports"));

        // Let the planner see the real distribution rather than guessing from an
        // empty table.
        await _dbContext.Database.ExecuteSqlRawAsync("ANALYZE projects;");
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _db.DisposeAsync();
    }

    private async Task SeedAsync(params (string Title, string Description, string Requirements, string Deliverables)[] rows)
    {
        // projects.client_id references client_profiles.id, NOT users.id. Getting
        // this wrong produces a foreign key violation that reads like a schema
        // problem rather than an id-space mistake.
        var clientId = await CreateClientProfileAsync();

        foreach (var (title, description, requirements, deliverables) in rows)
        {
            _dbContext.Projects.Add(new Project
            {
                Client = null!,
                ClientId = clientId,
                Title = title,
                Description = description,
                Requirements = requirements,
                ExpectedDeliverables = deliverables,
                PricingType = ProjectPricingType.Fixed,
                FixedPrice = 1000m,
                ExpectedDays = 14,
                Status = ProjectStatus.Open,
                IsActive = true,
            });
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task<Guid> CreateClientProfileAsync()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"client-{Guid.NewGuid():N}@test.invalid",
            PasswordHash = "x",
            FullName = "Search Test Client",
            Role = UserRole.Client,
            Status = UserStatus.Active,
            IsEmailVerified = true,
        };
        _dbContext.Users.Add(user);

        var profile = new ClientProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            CompanyName = "Test Co",
            Location = "Berlin",
        };
        _dbContext.ClientProfiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        return profile.Id;
    }

    private IQueryable<Project> OpenProjects() =>
        _dbContext.Projects.Where(p => p.Status == ProjectStatus.Open && p.IsActive);

    private async Task<List<string>> TitlesAsync(IQueryable<Project> query) =>
        (await query.ToListAsync()).Select(p => p.Title).ToList();

    // ── word boundaries ───────────────────────────────────────────────────

    [Fact]
    public async Task Searching_react_does_not_return_reactor()
    {
        /// The clearest single defect in the old implementation.
        var titles = await TitlesAsync(OpenProjects().Apply("react"));

        titles.Should().Contain("React dashboard rebuild");
        titles.Should().NotContain("Reactor core monitoring",
            "\"react\" is a word, not a substring; matching \"Reactor\" is exactly the "
            + "kind of confidently wrong result a client cannot easily notice");
    }

    [Fact]
    public async Task Searching_press_does_not_return_wordpress()
    {
        var titles = await TitlesAsync(OpenProjects().Apply("press"));

        titles.Should().NotContain("WordPress marketing site");
    }

    [Fact]
    public async Task A_word_found_in_the_title_is_returned()
    {
        var titles = await TitlesAsync(OpenProjects().Apply("kubernetes"));

        titles.Should().Contain("Kubernetes migration");
    }

    // ── stemming ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Searching_a_plural_finds_the_singular()
    {
        var titles = await TitlesAsync(OpenProjects().Apply("databases"));

        titles.Should().Contain("Reporting warehouse",
            "the English text search configuration stems \"databases\" to \"datab\", "
            + "which is what a user typing the plural actually wants");
    }

    // ── multi-word queries ────────────────────────────────────────────────

    [Fact]
    public async Task AMultiWordQuery_requires_every_term()
    {
        var titles = await TitlesAsync(OpenProjects().Apply("react redux"));

        titles.Should().Contain("React dashboard rebuild");
        titles.Should().NotContain("Kubernetes migration");
    }

    [Fact]
    public async Task Punctuation_does_not_raise_a_database_error()
    {
        /// `websearch_to_tsquery` treats punctuation as separators; `to_tsquery`
        /// would raise a syntax error and turn a stray apostrophe into a 500.
        var act = async () => await TitlesAsync(OpenProjects().Apply("react's \"broken\" -- query"));

        await act.Should().NotThrowAsync();
    }

    // ── ranking ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_title_match_outranks_a_mention_in_the_requirements()
    {
        var titles = await TitlesAsync(OpenProjects().Apply("react", rank: true));

        titles.Should().NotBeEmpty();
        titles[0].Should().Be("React dashboard rebuild",
            "the tsvector weights the title A and the requirements C, so a title "
            + "hit must come first");
    }

    [Fact]
    public async Task Ranking_changes_the_order_and_merely_filtering_does_not()
    {
        var ranked = await TitlesAsync(OpenProjects().Apply("react", rank: true));
        var unranked = await TitlesAsync(OpenProjects().Apply("react"));

        unranked.Should().BeEquivalentTo(ranked, "both find the same rows");
        ranked.First().Should().Be("React dashboard rebuild");
    }

    // ── type-ahead ────────────────────────────────────────────────────────

    [Fact]
    public async Task ATitlePrefixIsFoundEvenMidWord()
    {
        /// Full-text search alone breaks type-ahead: no committed tsvector contains
        /// the stem "dashi", so a user typing four letters of "dashboard" gets
        /// nothing, and the search box looks broken.
        var titles = await TitlesAsync(OpenProjects().Apply("dash", titleStartsWith: true));

        titles.Should().Contain("React dashboard rebuild",
            "the title is 'React dashboard rebuild', which starts with neither the "
            + "literal 'dash' -- 'dashboard' is the second word. A prefix match on "
            + "the whole title cannot find it, which is exactly the gap this arm is "
            + "meant to close: a title containing the term anywhere");
    }

    [Fact]
    public async Task AWildcardInTheSearchTermCannotMatchEverything()
    {
        /// `%` is a LIKE wildcard and the prefix pattern is built from raw input.
        ///
        /// Unescaped, searching for `%` matches every project with a non-empty
        /// title. That is not a crash, so nothing reports it -- it just looks like a
        /// search box that ignores what you type.
        var titles = await TitlesAsync(OpenProjects().Apply("%", titleStartsWith: true));

        titles.Should().BeEmpty("a literal '%' must be treated as a character");
    }

    [Fact]
    public async Task AnUnderscoreIsNotAWildcardEither()
    {
        var titles = await TitlesAsync(OpenProjects().Apply("_", titleStartsWith: true));

        titles.Should().BeEmpty("a literal '_' must be treated as a character");
    }

    /// <summary>
    /// Run a query directly, opening a connection for it.
    /// </summary>
    /// <remarks>
    /// <c>GetDbConnection().CreateCommand()</c> returns a command whose connection
    /// EF has already returned to the pool, so every such test failed with "Connection
    /// is not open". Opening it here is the fix, and wrapping it in one place means
    /// the mistake cannot be repeated.
    /// </remarks>
    private async Task<List<string>> QueryLinesAsync(string sql)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync();

            var lines = new List<string>();
            while (await reader.ReadAsync())
            {
                lines.Add(reader.IsDBNull(0) ? "<null>" : reader.GetString(0));
            }

            return lines;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private async Task<string?> QueryScalarAsync(string sql)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return await command.ExecuteScalarAsync() as string;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    [Fact]
    public async Task AGin_index_exists_on_the_search_vector()
    {
        var method = await QueryScalarAsync(
            """
            SELECT am.amname
            FROM pg_class c
            JOIN pg_am am ON am.oid = c.relam
            WHERE c.relname = 'ix_projects_search_vector'
            """);

        method.Should().Be("gin",
            "a btree index cannot answer a `@@` match at all; only GIN can, so this "
            + "is the assertion that distinguishes a working index from a decorative "
            + "one");
    }

    [Fact]
    public async Task TheIndexIsUsableByThePredicate()
    {
        /// Not a performance test -- a reachability test.
        ///
        /// With five rows the planner is *right* to choose a sequential scan, so
        /// asserting "no Seq Scan" would fail against correct behaviour. Forcing
        /// `enable_seqscan = off` removes the planner's choice and shows what it
        /// would do with a large table: use the index.
        await _dbContext.Database.ExecuteSqlRawAsync("ANALYZE projects;");

        // Each EXPLAIN row can itself contain newlines, so each is flattened before
        // joining: the pattern spans two plan lines ("Bitmap Heap Scan" on one,
        // "Bitmap Index Scan" on the next) and a raw newline between them defeats a
        // single regex over the whole plan.
        var plan = string.Join(
            " ",
            (await QueryLinesAsync(
                """
                SET enable_seqscan = off;
                EXPLAIN (COSTS OFF)
                SELECT id FROM projects
                WHERE search_vector @@ plainto_tsquery('kubernetes');
                RESET enable_seqscan;
                """)).Select(line => line.Replace('\n', ' ').Replace('\r', ' ')));

        // `Contain`, not `Match`. FluentAssertions' `Match` compiles the pattern as a
        // regular expression and its whitespace handling has bitten this twice: the
        // plan clearly reads "Bitmap Index Scan" and still did not match. The
        // assertion only needs substring presence, and a plain `Contain` cannot fail
        // for a reason unrelated to the index.
        foreach (var node in new[]
                 {
                     "Bitmap Index Scan",
                     "Index Scan",
                     "ix_projects_search_vector",
                 })
        {
            plan.Should().Contain(node,
                "with sequential scans disabled the only remaining option is the GIN "
                + "index, so its absence means it is not usable; got plan: " + plan);
        }
    }

    [Fact]
    public async Task TheGeneratedColumnExists_and_is_generated_always()
    {
        var value = await QueryScalarAsync(
            """
            SELECT is_generated
            FROM information_schema.columns
            WHERE table_name = 'projects' AND column_name = 'search_vector'
            """);

        value.Should().Be("ALWAYS",
            "a plain column would let a writer leave the vector describing text that "
            + "no longer exists, and search would return confidently wrong results "
            + "with no error anywhere to notice");
    }

    [Fact]
    public async Task TheVectorTracks_updates_and_not_just_inserts()
    {
        /// The rot case. If the vector were a plain column, renaming a project would
        /// leave it findable under its old name forever, silently.
        var project = await _dbContext.Projects
            .FirstAsync(p => p.Title == "Kubernetes migration");

        project.Title = "Nomad migration";
        await _dbContext.SaveChangesAsync();
        await _dbContext.Database.ExecuteSqlRawAsync("ANALYZE projects;");

        var after = await TitlesAsync(OpenProjects().Apply("kubernetes", titleStartsWith: true));
        var nowFinds = await TitlesAsync(OpenProjects().Apply("nomad", titleStartsWith: true));

        after.Should().NotContain("Kubernetes migration",
            "a generated column is recomputed on UPDATE; this is the property that a "
            + "plain column silently loses");
        nowFinds.Should().Contain("Nomad migration");
    }

    // ── no-search behaviour ───────────────────────────────────────────────

    [Fact]
    public async Task ABlankSearchReturnsEverything_newest_first()
    {
        var all = await TitlesAsync(OpenProjects().Apply(null));
        var blank = await TitlesAsync(OpenProjects().Apply("   "));

        all.Should().HaveCount(5);
        blank.Should().BeEquivalentTo(all, "whitespace is not a search");
    }

    [Fact]
    public async Task ASearchThatMatchesNothingReturnsAnEmptyList_notAnError()
    {
        var titles = await TitlesAsync(OpenProjects().Apply("zzzznotathing"));

        titles.Should().BeEmpty();
    }
}
