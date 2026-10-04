using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Text;
using WebOfWork.Api.Data;
using Xunit;

namespace WebOfWork.Api.Tests.Documentation;

/// <summary>
/// Generates <c>ER-DIAGRAM.md</c> from the live EF model, and fails if the
/// committed file differs.
///
/// ## Why generated rather than drawn
///
/// A hand-drawn ER diagram is correct on the day it is drawn. Then somebody adds
/// a column, or a foreign key, or an entity, and the diagram stays wrong — and
/// nothing reports it, because a picture of a database is not compared against
/// anything. By the time anyone notices, it is being used to explain the schema
/// to a marker, which is the worst possible moment to discover it describes a
/// database from two months ago.
///
/// So the diagram is a snapshot: derived from the same model EF uses to create
/// the migrations, which is the only definition of the schema that cannot be out
/// of date. The test writes the file and then fails if the bytes changed. The
/// developer's next step is to read the diff — which is a schema review, in
/// effect — and commit it.
///
/// ## Why no database is needed
///
/// The model is built from the entity configurations, not from the database.
/// <c>UseNpgsql</c> with a connection string that points nowhere is enough: no
/// connection is opened, because nothing here queries anything. That matters —
/// a documentation generator that needs a running database is a documentation
/// generator nobody runs.
/// </summary>
public class ErDiagramGeneratorTests
{
    /// <summary>
    /// Repository root, found by walking up from the test assembly until the
    /// solution file appears. Hardcoding a relative path would break the moment
    /// the test project moved, and CI checks out to a different absolute path
    /// than a developer has.
    /// </summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WebOfWork.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate WebOfWork.sln above " + AppContext.BaseDirectory);
    }

    private static WebOfWorkDbContext BuildModel()
    {
        // Deliberately unreachable. The model is built from the configurations;
        // no connection is opened because nothing here queries.
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseSnakeCaseNamingConvention()
            .UseNpgsql("Host=localhost;Database=model_only;Username=none;Password=none")
            .Options;

        return new WebOfWorkDbContext(options);
    }

    private static string Render()
    {
        using var context = BuildModel();

        // `GetTableName()` rather than the CLR name, because the diagram is
        // documentation for someone reading the database, and the database calls
        // the table `freelancer_portfolios`.
        var entityTypes = context.Model
            .GetEntityTypes()
            .Where(e => e.GetTableName() is not null)
            .OrderBy(e => e.GetTableName(), StringComparer.Ordinal)
            .ToList();

        var builder = new StringBuilder();

        builder.AppendLine("<!--");
        builder.AppendLine("  GENERATED FILE - DO NOT EDIT BY HAND.");
        builder.AppendLine();
        builder.AppendLine("  Produced from the EF Core model by");
        builder.AppendLine("  Tests/WebOfWork.Api.Tests/Documentation/ErDiagramGeneratorTests.cs");
        builder.AppendLine();
        builder.AppendLine("  Run `dotnet test` to regenerate. If this test fails, the schema");
        builder.AppendLine("  changed: read the diff, then commit the updated file. That diff is");
        builder.AppendLine("  a schema review.");
        builder.AppendLine("-->");
        builder.AppendLine();
        builder.AppendLine("# Database schema");
        builder.AppendLine();
        builder.AppendLine($"{entityTypes.Count} entities, generated from the Entity Framework Core model.");
        builder.AppendLine("A hand-drawn diagram cannot be verified against the code; this one is the");
        builder.AppendLine("model itself. Table and column names are what PostgreSQL stores, not the C#");
        builder.AppendLine("names — naming is applied by `SnakeCaseLower`.");
        builder.AppendLine();

        builder.AppendLine("```mermaid");
        builder.AppendLine("erDiagram");

        foreach (var entity in entityTypes)
        {
            var table = entity.GetTableName()!;

            // Mermaid reserves `o`, `}` and `"` in entity names; table names are
            // ours and contain none, but a space would silently break the block.
            var safeTable = table.Replace(' ', '_');

            var keyColumns = entity.FindPrimaryKey()?.Properties
                .Select(p => p.Name)
                .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>();

            var foreignKeys = entity.GetForeignKeys().ToList();
            var foreignKeyColumns = foreignKeys
                .SelectMany(fk => fk.Properties)
                .Select(p => p.Name)
                .ToHashSet(StringComparer.Ordinal);

            var properties = entity.GetProperties()
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToList();

            if (properties.Count == 0)
            {
                builder.AppendLine($"    {safeTable} {{ }}");
                continue;
            }

            builder.AppendLine($"    {safeTable} {{");

            foreach (var property in properties)
            {
                var column = property.GetColumnName();
                var type = DiagramType(property.GetColumnType());

                // A comma inside the type would end the Mermaid field early.
                // `numeric(18,4)` is exactly that case.
                type = type.Replace(",", "_");

                var key = keyColumns.Contains(property.Name)
                    ? "PK"
                    : foreignKeyColumns.Contains(property.Name) ? "FK" : "";

                builder.AppendLine($"        {DiagramTypeName(property)} {type} {key}".TrimEnd());
            }

            builder.AppendLine("    }");
        }

        // Relationships after the entity blocks, so a reader meets the shape
        // before the wiring.
        foreach (var entity in entityTypes)
        {
            var table = entity.GetTableName()!.Replace(' ', '_');

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                var principal = foreignKey.PrincipalEntityType.GetTableName();
                if (principal is null)
                {
                    continue;
                }

                var name = foreignKey.GetConstraintName() ?? string.Join("_", foreignKey.Properties);
                builder.AppendLine(
                    $"    {table} }}o--|| {principal.Replace(' ', '_')} : \"{DiagramTypeName(foreignKey)}\"");
            }
        }

        builder.AppendLine("```");
        builder.AppendLine();
        // Naming, stated rather than assumed.
        //
        // Every table is snake_case because `Program.cs` applies
        // `UseSnakeCaseNamingConvention()` to the Npgsql provider. That is one
        // line in one file and it is invisible from the entities: none of them
        // declares `ToTable`, so without the convention EF falls back to the
        // DbSet property name and the model describes PascalCase tables that no
        // migration creates.
        //
        // That mistake was made here. An earlier version of this generator built
        // its context with `.UseNpgsql(...)` alone and reported fourteen entities
        // as misnamed and the schema as inconsistent with the migrations -- a
        // confident, specific and entirely false account, which is the worst kind
        // of documentation error there is. The count is emitted rather than
        // assumed, so dropping the convention changes a number in this file, and
        // `Every_table_is_snake_case` below fails.
        var snakeCased = entityTypes.Count(e =>
        {
            var table = e.GetTableName()!;
            return table == table.ToLowerInvariant() && table.Any(char.IsLetter);
        });

        builder.AppendLine("## Table naming");
        builder.AppendLine();
        builder.AppendLine($"{snakeCased} of {entityTypes.Count} tables are `snake_case`.");
        builder.AppendLine();
        builder.AppendLine("Applied globally by `UseSnakeCaseNamingConvention()` in `Program.cs` rather");
        builder.AppendLine("than per entity, so a new entity needs no `ToTable` call and cannot introduce a");
        builder.AppendLine("differently-named table.");

        builder.AppendLine();
        builder.AppendLine("## Reading it");
        builder.AppendLine();
        builder.AppendLine("- **PK** primary key, **FK** foreign key. A column can be both.");
        builder.AppendLine("- `o|` means zero-or-one, `||` means exactly one. Most relations here are");
        builder.AppendLine("  many-to-one: many rows point at one parent.");
        builder.AppendLine("- `uuid`/`timestamptz`/`jsonb` are the PostgreSQL types as stored, not the");
        builder.AppendLine("  C# types (`Guid`, `DateTime`, `string`).");
        builder.AppendLine();
        builder.AppendLine("## Entities without a table");
        builder.AppendLine();
        builder.AppendLine("Join and owned types are omitted: they have no table of their own and");
        builder.AppendLine("appear only as columns on the table that owns them.");

        return builder.ToString().Replace("\r\n", "\n");
    }

    /// <summary>
    /// A type safe to put in a Mermaid field.
    /// </summary>
    private static string DiagramType(string? columnType)
    {
        if (string.IsNullOrWhiteSpace(columnType))
        {
            return "unknown";
        }

        return columnType;
    }

    /// <summary>
    /// The property name, with Mermaid-hostile characters removed.
    /// </summary>
    private static string DiagramTypeName(object member)
    {
        var name = member switch
        {
            Microsoft.EntityFrameworkCore.Metadata.IProperty property => property.Name,

            // The FK's own property names, not its constraint name. A constraint
            // name reads `FK_ChatRooms_Users_ClientId`, which restates the
            // relationship three times and is useless as a label; `ClientId` says
            // which column carries it, which is the thing a reader needs.
            Microsoft.EntityFrameworkCore.Metadata.IForeignKey key =>
                string.Join("_", key.Properties.Select(p => p.Name)),

            _ => member.ToString() ?? "unknown",
        };

        return new string(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
    }

    [Fact]
    public void The_committed_er_diagram_matches_the_model()
    {
        var path = Path.Combine(RepositoryRoot(), "ER-DIAGRAM.md");
        var rendered = Render();

        if (!File.Exists(path))
        {
            File.WriteAllText(path, rendered);
            Assert.Fail(
                "ER-DIAGRAM.md did not exist and has been generated. Read it, then commit it.");
        }

        var committed = File.ReadAllText(path).Replace("\r\n", "\n");

        if (committed == rendered)
        {
            return;
        }

        // Write it anyway. The fix is to read the diff and commit it, and a
        // developer who has to reconstruct the file by hand is a developer who
        // will not run the test.
        File.WriteAllText(path, rendered);

        var summary = DescribeChange(committed, rendered);

        Assert.Fail(
            "ER-DIAGRAM.md was out of date and has been regenerated. The schema changed:\n\n"
            + summary
            + "\n\nRead the diff and commit the updated file. If the change was not "
            + "intended, a migration is missing.");
    }

    /// <summary>
    /// What changed, in terms a person can act on.
    ///
    /// A raw diff of a generated file is unreadable — it is hundreds of lines of
    /// reordered attributes. Naming the entities that appeared, vanished, or
    /// gained or lost a column is the difference between a useful failure and a
    /// wall of text nobody reads before regenerating and moving on.
    /// </summary>
    private static string DescribeChange(string before, string after)
    {
        var lines = new List<string>();

        var beforeBlocks = Blocks(before);
        var afterBlocks = Blocks(after);

        foreach (var table in afterBlocks.Keys.Except(beforeBlocks.Keys).OrderBy(x => x))
        {
            lines.Add($"  + table {table} ({afterBlocks[table].Count} columns)");
        }

        foreach (var table in beforeBlocks.Keys.Except(afterBlocks.Keys).OrderBy(x => x))
        {
            lines.Add($"  - table {table} removed");
        }

        foreach (var table in afterBlocks.Keys.Intersect(beforeBlocks.Keys).OrderBy(x => x))
        {
            var added = afterBlocks[table].Except(beforeBlocks[table]).ToList();
            var removed = beforeBlocks[table].Except(afterBlocks[table]).ToList();

            foreach (var column in added)
            {
                lines.Add($"  + {table}.{column}");
            }

            foreach (var column in removed)
            {
                lines.Add($"  - {table}.{column}");
            }
        }

        return lines.Count == 0
            ? "  (no entity or column change — the difference is formatting; commit as-is)"
            : string.Join("\n", lines);
    }

    private static Dictionary<string, List<string>> Blocks(string markdown)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        string? current = null;

        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.Trim();

            // An entity block opens with four spaces and ends the attribute list.
            if (trimmed.StartsWith("    ") && !trimmed.Contains(' ') &&
                !trimmed.EndsWith("}", StringComparison.Ordinal) &&
                current is null)
            {
                var name = trimmed.Trim();
                if (name.Length > 0 && !name.Contains("}"))
                {
                    current = name;
                    result[current] = new List<string>();
                    continue;
                }
            }

            if (trimmed == "}")
            {
                current = null;
                continue;
            }

            if (current is not null && trimmed.StartsWith("        ", StringComparison.Ordinal))
            {
                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    result[current].Add(parts[1]);
                }
            }
        }

        return result;
    }

    [Fact]
    public void Every_table_is_snake_case()
    {
        // A guard, not a description.
        //
        // The snake_case naming comes from `UseSnakeCaseNamingConvention()` in
        // `Program.cs`. Nothing in the entities records it, so removing it would
        // not fail a build -- the application would keep working against the
        // existing database and every future migration would create PascalCase
        // tables alongside it.
        //
        // This test is the thing that notices, and it only works because the
        // context is configured the same way the application's is. An earlier
        // version of this generator omitted that call and produced a diagram --
        // and a written finding -- that was confidently wrong about fourteen
        // tables. The context configuration is therefore mirrored in
        // `BuildModel`, and this assertion is what proves it.
        using var context = BuildModel();

        var offenders = context.Model
            .GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Where(name => name.Any(char.IsUpper))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "table names come from UseSnakeCaseNamingConvention() in Program.cs. If this fails, "
            + "that call was removed -- or this context was configured differently from the "
            + "application's, which is the mistake that produced a false finding once already.");
    }

    [Fact]
    public void The_diagram_covers_every_mapped_entity()
    {
        // A separate assertion from the snapshot, because the snapshot can pass
        // while omitting something: if the generator silently skipped a table,
        // the committed file would match the incomplete output forever.
        using var context = BuildModel();

        var expected = context.Model
            .GetEntityTypes()
            .Count(e => e.GetTableName() is not null);

        var rendered = Render();

        Assert.Contains("```mermaid", rendered);
        Assert.Equal(expected, CountEntityBlocks(rendered));

        // And the file on disk must actually contain all of them.
        var path = Path.Combine(RepositoryRoot(), "ER-DIAGRAM.md");
        Assert.True(File.Exists(path), "ER-DIAGRAM.md is missing");

        var committed = File.ReadAllText(path);
        foreach (var entity in context.Model.GetEntityTypes().Where(e => e.GetTableName() is not null))
        {
            Assert.Contains(entity.GetTableName()!, committed);
        }
    }

    private static int CountEntityBlocks(string markdown)
    {
        var count = 0;
        foreach (var line in markdown.Split('\n'))
        {
            if (line.TrimEnd() == "    }")
            {
                count++;
            }
        }

        return count;
    }
}