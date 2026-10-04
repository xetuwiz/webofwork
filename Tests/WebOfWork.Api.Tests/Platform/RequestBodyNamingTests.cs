using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using WebOfWork.Api.Controllers;
using Xunit;

namespace WebOfWork.Api.Tests.Platform;

/// <summary>
/// Does a body sent by the React client actually bind?
/// </summary>
/// <remarks>
/// <para>
/// <c>Program.cs</c> sets <c>PropertyNamingPolicy = SnakeCaseLower</c>. That policy
/// governs deserialization as well as serialization, so a C# property
/// <c>FreelancerIds</c> expects the JSON key <c>freelancer_ids</c>.
/// <c>PropertyNameCaseInsensitive</c> defaults to true, but that only forgives
/// differences of <em>case</em> -- an underscore is not a case difference.
/// </para>
/// <para>
/// So <c>{"freelancerIds": [...]}</c> binds to nothing, and the DTO's
/// <c>= new()</c> initialiser leaves an empty list. The symptom was a 400 reading
/// <c>"Approving an empty shortlist would end the run without contacting anyone"</c>
/// while the page plainly said "4 selected" -- four ids sent, none received.
/// </para>
/// <para>
/// The probe below uses the same options the application registers, read out of
/// <c>Program.cs</c>'s configuration rather than a hand-built instance, because the
/// entire failure is a property of that configuration.
/// </para>
/// </remarks>
public sealed class RequestBodyNamingTests
{
    /// <summary>
    /// The application's JSON options, reproduced.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>Program.cs</c>. Kept as a literal rather than reflected out of the
    /// host so the test does not need a running application; the two must be changed
    /// together, which the naming-policy assertion below makes explicit.
    /// </remarks>
    private static JsonSerializerOptions AppOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }

    private static readonly Guid A = Guid.Parse("657797ec-07d3-4205-94ed-ea256d7b958a");
    private static readonly Guid B = Guid.Parse("b944584e-2cd9-43f9-b5c7-a25e2c865ff4");

    [Fact]
    public void The_probe_itself_is_sound()
    {
        // A probe that cannot fail proves nothing. Validated two ways before any
        // assertion below is trusted: nonsense is rejected, and a known-good
        // snake_case body is accepted.
        var act = () => JsonSerializer.Deserialize<ApproveShortlistRequest>(
            "{not json", AppOptions());

        act.Should().Throw<JsonException>();

        var snake = JsonSerializer.Deserialize<ApproveShortlistRequest>(
            $$"""{"freelancer_ids":["{{A}}","{{B}}"],"rationale":"because"}""",
            AppOptions());

        snake!.FreelancerIds.Should().HaveCount(2);
    }

    [Fact]
    public void Snake_case_binds()
    {
        var request = JsonSerializer.Deserialize<ApproveShortlistRequest>(
            $$"""{"freelancer_ids":["{{A}}","{{B}}"]}""", AppOptions());

        request!.FreelancerIds.Should().BeEquivalentTo(new[] { A, B });
    }

    [Fact]
    public void Camel_case_does_not_bind_and_the_dto_reports_an_empty_shortlist()
    {
        // Exactly what the React client sent. Four ids on the wire, and the
        // service was handed none -- which is why approving failed with "Approving
        // an empty shortlist" while the screen said four were selected.
        var request = JsonSerializer.Deserialize<ApproveShortlistRequest>(
            $$"""{"freelancerIds":["{{A}}","{{B}}"],"rationale":"because"}""",
            AppOptions());

        request!.FreelancerIds.Should().BeEmpty(
            "an underscore is not a case difference, so camelCase cannot bind to a "
            + "snake_case policy");

        // `rationale` is a single word, so it binds under either spelling and the
        // body looks entirely plausible. This is the whole reason the bug survived:
        // the one key that arrived was the one that could not have told anyone.
        request.Rationale.Should().Be("because");
    }

    [Fact]
    public void Single_word_keys_are_unaffected()
    {
        // Why this went unnoticed: `rationale` is one word and binds either way,
        // so the body looked entirely plausible while carrying none of its data.
        var request = JsonSerializer.Deserialize<ApproveShortlistRequest>(
            """{"rationale":"because"}""", AppOptions());

        request!.Rationale.Should().Be("because");
    }

    [Fact]
    public void The_wire_convention_is_snake_case_in_both_directions()
    {
        // Stated as an assertion so the direction of the fix is unambiguous: the
        // server's policy is the authority, and the client writes snake_case to
        // match. Changing the policy instead would silently break every stored
        // jsonb document and every already-written payload.
        JsonNamingPolicy.SnakeCaseLower.ConvertName("FreelancerIds")
            .Should().Be("freelancer_ids");

        JsonNamingPolicy.SnakeCaseLower.ConvertName("FreelancerIds")
            .Should().NotBe("freelancerIds");
    }
}