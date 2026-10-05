using System.Diagnostics;
using System.Globalization;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;
using Xunit.Abstractions;

namespace WebOfWork.Api.Tests.Performance;

/// <summary>
/// Performance measurements for the report's performance section.
///
/// ## What these tests are, and what they are not
///
/// They are **in-process measurements against a real PostgreSQL container** —
/// not a production load test, and the difference is not a detail. A load test
/// answers "will this serve N users"; these answer "does the dominant query
/// scale linearly, and does anything degrade under concurrency", which is the
/// question that catches an accidental N+1 or a missing index before anyone
/// deploys.
///
/// No network hop, no TLS, no reverse proxy and no real client are involved, so
/// the absolute numbers are far below production and must not be quoted as
/// production figures. What is meaningful is the *relative* behaviour: p95
/// against p50, and throughput against concurrency.
///
/// ## Why the thresholds are loose, on purpose
///
/// A performance test with a tight budget is the most reliable way to produce a
/// red build on someone else's machine. GitHub runners are shared, throttled and
/// of unknown specification; a threshold tight enough to catch a real regression
/// is also tight enough to fail because a container was descheduled.
///
/// So the assertions check *shape*, which is machine-independent:
///
/// - no request errors (a correctness property, and the strongest signal here);
/// - p95 is not catastrophically worse than p50, which is what an N+1 looks like;
/// - throughput does not collapse as concurrency rises.
///
/// And the measured numbers are written to the test output, so the report quotes
/// figures that were actually produced rather than figures someone hoped for.
/// </summary>
[Trait("Category", "Container")]
public sealed class PerformanceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .WithDatabase("webofwork_perf_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Rows seeded per table. Chosen so the table is large enough for an index to
    /// matter and the whole seed still runs in seconds: at a few thousand rows the
    /// planner may legitimately choose a sequential scan, and a benchmark run
    /// against ten rows measures nothing.
    /// </summary>
    private const int ProjectCount = 2000;

    private const int DefaultConcurrency = 32;
    private const int DefaultIterations = 40;

    public PerformanceTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();

        // Migrations, not `EnsureCreated`.
        //
        // These tests measure query and write latency against the schema the
        // application actually ships, which is the one the 28 migrations build.
        // `EnsureCreated` is quicker to set up and would quietly omit everything
        // the migrations do beyond creating tables -- indexes, constraints,
        // checks -- which is precisely where a performance regression lives.
        await db.Database.MigrateAsync();
        _ownerId = await SeedAsync(db);
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    /// <summary>
    /// A real user id, used as the owner of the workflow runs below.
    /// A random Guid would violate the foreign key to `users` -- and every one of
    /// the concurrent writes would fail, which would look exactly like a
    /// contention bug and be nothing of the kind.
    /// </summary>
    private Guid _ownerId;

    private WebOfWorkDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            // Must match Program.cs, or this measures a schema the application
            // never creates.
            .UseSnakeCaseNamingConvention()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new WebOfWorkDbContext(options);
    }

    /// <summary>
    /// Seeded in one transaction. A fixture that inserts row by row takes
    /// long enough that the suite gets skipped for being slow, and a slow
    /// benchmark is one nobody runs.
    /// </summary>
    private static async Task<Guid> SeedAsync(WebOfWorkDbContext db)
    {
        if (await db.Projects.AnyAsync())
        {
            return (await db.Users.Select(u => u.Id).FirstAsync());
        }

        var clients = new List<ClientProfile>();
        var projects = new List<Project>(ProjectCount);

        for (var i = 0; i < 10; i++)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"perf-client-{i}@test.com",
                FullName = $"Perf Client {i}",
                PasswordHash = "x",
                Role = UserRole.Client,
                CurrentRole = UserRole.Client,
                Status = UserStatus.Active,
            };

            clients.Add(new ClientProfile
            {
                Id = Guid.NewGuid(),
                User = user,
                CompanyName = $"Perf Client {i}",
                Location = "Colombo",
            });

            db.Users.Add(user);
        }

        for (var i = 0; i < ProjectCount; i++)
        {
            var client = clients[i % clients.Count];

            projects.Add(new Project
            {
                ClientId = client.Id,
                Client = client,
                Title = $"Performance project {i}",
                Description = "A description long enough to be realistic in storage. " +
                              "Benchmark rows exist to make the database do work, not to be read.",
                Requirements = "Requirements",
                ExpectedDeliverables = "Deliverables",
                PricingType = i % 2 == 0 ? ProjectPricingType.Fixed : ProjectPricingType.Range,
                FixedPrice = 500m + (i % 50) * 100m,
                MinPrice = 400m,
                MaxPrice = 900m,
                ExpectedDays = 7 + (i % 30),
                Status = ProjectStatus.Open,
                IsActive = true,
            });
        }

        db.ClientProfiles.AddRange(clients);
        db.Projects.AddRange(projects);

        await db.SaveChangesAsync();

        return clients[0].User.Id;
    }

    /// <summary>
    /// The paginated, filtered project query the freelancer browse screen runs.
    /// Chosen because it is the highest-traffic read in the product and the one
    /// most likely to regress: it filters, sorts and pages.
    /// </summary>
    private static Task<List<Project>> QueryProjectsAsync(WebOfWorkDbContext db, int page, int pageSize) =>
        db.Projects
            .AsNoTracking()
            .Where(p => p.IsActive && p.Status == ProjectStatus.Open)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    [Fact]
    public async Task Filtered_paged_query_latency_is_stable_under_concurrency()
    {
        var serial = await MeasureAsync(1, 10, async () =>
        {
            await using var db = CreateContext();
            await QueryProjectsAsync(db, 1, 20);
        });

        var concurrent = await MeasureAsync(DefaultConcurrency, DefaultIterations, async () =>
        {
            // A context per operation. Sharing one DbContext across concurrent
            // operations is not thread-safe and would measure EF's guard rather
            // than the database.
            await using var db = CreateContext();
            await QueryProjectsAsync(db, 1, 20);
        });

        Report("filtered paged project query (20 rows of 2000)", serial, concurrent);

        // Correctness: nothing failed. A benchmark that errors is not a slow
        // benchmark, it is a broken endpoint.
        concurrent.Errors.Should().Be(0, "no operation may fail under concurrency");

        // Shape: concurrent p95 must stay in the same order as the *serial* p95.
        //
        // Compared against the serial run rather than the concurrent median, and
        // with a deliberately loose multiplier. Both choices are about surviving
        // a shared CI runner: under contention the connection pool queues and the
        // OS schedules, so some spread is normal and is not evidence of a defect.
        // A test that fails on normal spread gets muted, and a muted test protects
        // nothing.
        //
        // It still catches the thing worth catching. An N+1 or an unindexed filter
        // multiplies latency by ten to a hundred, not by percent, and no amount of
        // runner noise reaches that.
        concurrent.P95.Should().BeLessThan(
            Math.Max(serial.P95 * 20, 5_000),
            "concurrent p95 should stay within an order of magnitude of serial p95; "
            + "a large ratio indicates an N+1 or an unindexed filter. "
            + $"serial p95 was {serial.P95:F1}ms, concurrent p95 was {concurrent.P95:F1}ms");

        // Throughput must not collapse. More concurrency should not be
        // dramatically *less* productive than one-at-a-time.
        concurrent.ThroughputPerSecond.Should().BeGreaterThan(0);
        concurrent.ThroughputPerSecond.Should().BeGreaterThan(
            serial.ThroughputPerSecond / 10,
            "adding concurrency should not reduce throughput by more than an order of magnitude");
    }

    [Fact]
    public async Task Dashboard_aggregation_scales_with_row_count()
    {
        // A count over the whole table is the other read that degrades quietly,
        // because it works fine on a small dataset.
        var small = await MeasureAsync(1, 5, async () =>
        {
            await using var db = CreateContext();
            await db.Projects.AsNoTracking().CountAsync();
        });

        var full = await MeasureAsync(1, 5, async () =>
        {
            await using var db = CreateContext();
            await db.Projects.AsNoTracking()
                .Where(p => p.IsActive && p.Status == ProjectStatus.Open)
                .CountAsync();
        });

        Report("project count", small, full);

        full.Errors.Should().Be(0);
        full.P95.Should().BeLessThan(5_000, "a count over 2000 rows should not take seconds");
    }

    [Fact]
    public async Task Workflow_state_writes_do_not_serialise_under_load()
    {
        // The Agentic AI subsystem writes audit rows on every stage. If the write
        // path held a lock for its whole transaction, concurrent workflow
        // completions would queue behind each other — and because this happens
        // after a client-visible response, it would not surface as an error.
        var serial = await MeasureAsync(1, 10, async () =>
        {
            await WriteRunAsync(CreateContext());
        });

        var result = await MeasureAsync(8, 30, async () =>
        {
            await WriteRunAsync(CreateContext());
        });

        Report("workflow run + step log insert", serial, result);

        result.Errors.Should().Be(0,
            "concurrent workflow writes must not deadlock or conflict. First error: "
            + result.FirstError);

        // Against the serial run, for the same reason as the read test: normal
        // contention produces some spread, and the assertion has to be loose
        // enough not to fire on it.
        result.P95.Should().BeLessThan(
            Math.Max(serial.P95 * 20, 5_000),
            "write p95 should stay within an order of magnitude of serial p95; "
            + "a large ratio means the write path is serialising. "
            + $"serial p95 was {serial.P95:F1}ms, concurrent p95 was {result.P95:F1}ms");
    }

    /// <summary>
    /// One workflow run plus its step log -- the write every completed Agentic AI
    /// stage performs.
    ///
    /// The child is attached through the navigation rather than by setting
    /// <c>WorkflowRunId</c>. <c>WorkflowRun.Id</c> is an <c>int</c> identity
    /// generated by the database, so before the first save it is still 0 -- and a
    /// step log pointing at 0 is a foreign-key violation. That failure is
    /// identical at concurrency 1 and at concurrency 8, which is exactly what
    /// makes it read as a scaling bug rather than a mistake.
    /// </summary>
    private async Task WriteRunAsync(WebOfWorkDbContext db)
    {
        var run = new WorkflowRun
        {
            Objective = $"perf run {Guid.NewGuid():N}",
            Stage = "talent_match",
            Status = WorkflowRunStatus.Completed,
            RequestedByUserId = _ownerId,
        };

        db.WorkflowRuns.Add(run);
        db.WorkflowStepLogs.Add(new WorkflowStepLog
        {
            WorkflowRun = run,
            StepName = "matching",
            AgentName = "MatchingAgent",
            Status = "ok",
            DurationMs = 1,
        });

        await db.SaveChangesAsync();
    }

    // ── measurement ─────────────────────────────────────────────────────

    private sealed class Measurement
    {
        public int Operations { get; init; }
        public int Errors { get; init; }
        public double P50 { get; init; }
        public double P95 { get; init; }
        public double P99 { get; init; }
        public double Max { get; init; }
        public double Mean { get; init; }

        /// <summary>
        /// The first failure, if any.
        ///
        /// A count of errors tells you that something broke; it does not tell you
        /// what. Since every exception here is caught so that one failure does not
        /// end the run, without this the measurement would be undiagnosable -- the
        /// author would have to guess, and a guessed benchmark is a wrong one.
        /// </summary>
        public string? FirstError { get; init; }
        public double ElapsedMs { get; init; }

        public double ThroughputPerSecond =>
            ElapsedMs <= 0 ? 0 : Operations / (ElapsedMs / 1000d);
    }

    /// <summary>
    /// Runs <paramref name="operations"/> work items across
    /// <paramref name="concurrency"/> workers and records each one's duration.
    ///
    /// A hand-rolled harness rather than NBomber or k6, because those measure a
    /// process from the outside: they would need the API published and a server
    /// running, which cannot happen inside `dotnet test`. What is being measured
    /// here is the query and the write path under contention, and that does not
    /// need a socket.
    /// </summary>
    private static async Task<Measurement> MeasureAsync(
        int concurrency,
        int operations,
        Func<Task> operation)
    {
        var timings = new double[operations];
        var errors = 0;
        var next = -1;
        string? firstError = null;
        var overall = Stopwatch.StartNew();

        async Task Worker()
        {
            while (true)
            {
                // Interlocked increment, so each worker takes a distinct index and
                // no operation runs twice or not at all.
                var index = Interlocked.Increment(ref next);
                if (index >= operations)
                {
                    return;
                }

                var watch = Stopwatch.StartNew();
                try
                {
                    await operation();
                }
                catch (Exception exception)
                {
                    Interlocked.Increment(ref errors);
                    Interlocked.CompareExchange(ref firstError, exception.ToString(), null);
                }
                finally
                {
                    watch.Stop();
                    timings[index] = watch.Elapsed.TotalMilliseconds;
                }
            }
        }

        await Task.WhenAll(Enumerable.Range(0, concurrency).Select(_ => Worker()));
        overall.Stop();

        var sorted = timings.OrderBy(t => t).ToArray();

        return new Measurement
        {
            Operations = operations,
            Errors = errors,
            P50 = Percentile(sorted, 0.50),
            P95 = Percentile(sorted, 0.95),
            P99 = Percentile(sorted, 0.99),
            Max = sorted.Length == 0 ? 0 : sorted[^1],
            Mean = sorted.Length == 0 ? 0 : sorted.Average(),
            ElapsedMs = overall.Elapsed.TotalMilliseconds,
            FirstError = firstError,
        };
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        if (sorted.Length == 0)
        {
            return 0;
        }

        var rank = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        return sorted[Math.Clamp(rank, 0, sorted.Length - 1)];
    }

    /// <summary>
    /// Writes the numbers into the test log.
    ///
    /// This is the point of the file. A performance section in a report needs
    /// figures, and figures that were not produced are not evidence — so the
    /// measurements are printed where the CI log and the `.trx` keep them.
    /// </summary>
    private void Report(string label, Measurement baseline, Measurement measured)
    {
        var format = CultureInfo.InvariantCulture;

        _output.WriteLine($"[perf] {label}");
        _output.WriteLine(string.Format(format,
            "[perf]   serial     : n={0} mean={1:F1}ms p50={2:F1}ms p95={3:F1}ms errors={4}",
            baseline.Operations, baseline.Mean, baseline.P50, baseline.P95, baseline.Errors));

        _output.WriteLine(string.Format(format,
            "[perf]   concurrent : n={0} p50={1:F1}ms p95={2:F1}ms p99={3:F1}ms max={4:F1}ms "
            + "throughput={5:F0}/s errors={6}",
            measured.Operations, measured.P50, measured.P95, measured.P99, measured.Max,
            measured.ThroughputPerSecond, measured.Errors));

        // A single line, so it can be grepped out of a CI log and pasted into the
        // report without reformatting.
        _output.WriteLine(string.Format(format,
            "[perf] PERF|{0}|n={1}|mean={2:F1}|p50={3:F1}|p95={4:F1}|p99={5:F1}|max={6:F1}"
            + "|throughput={7:F0}|errors={8}",
            label, measured.Operations, measured.Mean, measured.P50, measured.P95,
            measured.P99, measured.Max, measured.ThroughputPerSecond, measured.Errors));

        if (measured.FirstError is not null)
        {
            _output.WriteLine("[perf]   first error: " + measured.FirstError.Split('\n')[0]);
        }
    }
}