using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using WebOfWork.Api;
using WebOfWork.Api.Controllers;
using Xunit;

namespace WebOfWork.Api.Tests.Platform;

/// <summary>
/// Can the API read back a decimal that arrives as a bare JSON number?
/// </summary>
/// <remarks>
/// <para>
/// <b>The failure.</b> <see cref="DecimalAsStringJsonConverter"/> is registered
/// globally in <c>Program.cs</c>, so it governs every decimal on every endpoint.
/// Its <c>Read</c> called <c>reader.GetString()</c> unconditionally. That
/// succeeds for a string token and throws for a number token -- and a decimal's
/// native JSON representation is a number. The converter could therefore only
/// read payloads it had itself written.
/// </para>
/// <para>
/// Observed live on run 24. The Python agent service sent
/// <c>{"project_id":11,"freelancer_id":"...","score":62.5231,...}</c> and got, four
/// times over:
///
/// <code>
/// $.score: The JSON value could not be converted to System.Decimal
/// </code>
///
/// so all four recommendations were dropped. The run still reported success,
/// validation passed, and the shortlist page rendered "No suggestions yet" --
/// an empty result presented as a considered one.
/// </para>
/// <para>
/// The write side stays string-valued on purpose: the Flutter and React clients
/// already parse that shape, and changing it would retype every price and score
/// in every response. Reading both forms is the asymmetry the format forces on us.
/// </para>
/// </remarks>
public sealed class DecimalAsStringJsonConverterTests
{
    /// <summary>
    /// Mirrors the options <c>Program.cs</c> registers, naming policy included.
    /// </summary>
    /// <remarks>
    /// The naming policy is load-bearing, and leaving it off is a trap worth
    /// naming. Without it <c>{"score": 62.5231}</c> does not bind to a property
    /// named <c>Score</c> at all: no match, no error, and every decimal reads as
    /// <c>0</c>. A probe written that way "confirms" that <c>true</c> and
    /// <c>[1]</c> convert to decimal perfectly happily, which is how a broken
    /// measurement gets mistaken for a working one.
    /// </remarks>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new DecimalAsStringJsonConverter() },
    };

    private sealed record Payload(decimal Score);

    private static decimal Read(string json)
        => JsonSerializer.Deserialize<Payload>(json, Options)!.Score;

    private static string Write(decimal value)
        => JsonSerializer.Serialize(new Payload(value), Options);

    public class ABareNumber
    {
        [Fact]
        public void Is_read_as_a_decimal()
        {
            // The exact shape the agent service sends.
            Read("""{"score":62.5231}""").Should().Be(62.5231m);
        }

        [Fact]
        public void Is_read_exactly_and_not_rounded_through_a_double()
        {
            // More significant digits than a double can hold. A
            // GetDouble()-based reader would return 0.1m here; GetDecimal()
            // returns every digit.
            Read("""{"score":0.1000000000000000055511151231257827}""")
                .Should().Be(0.1000000000000000055511151231257827m);
        }

        [Fact]
        public void Is_read_when_it_is_the_only_field()
        {
            Read("""{"score":1}""").Should().Be(1m);
        }

        [Fact]
        public void Negative_and_zero_are_not_special_cases()
        {
            Read("""{"score":-4.5}""").Should().Be(-4.5m);
            Read("""{"score":0}""").Should().Be(0m);
            Read("""{"score":0.0}""").Should().Be(0m);
        }

        [Fact]
        public void Exponent_notation_is_read()
        {
            // What json.dumps emits for very small or very large magnitudes.
            Read("""{"score":6.25231e+1}""").Should().Be(62.5231m);
        }
    }

    public class AString
    {
        [Fact]
        public void Is_still_read()
        {
            // The form we write. Must not regress when the number path is added.
            Read("""{"score":"62.52"}""").Should().Be(62.52m);
        }

        [Fact]
        public void Is_still_written()
        {
            // The response contract every client depends on.
            Write(62.5231m).Should().Be("""{"score":"62.52"}""");
        }

        [Fact]
        public void A_grouped_value_is_read_as_written_not_rescaled()
        {
            // InvariantCulture accepts a comma as a thousands separator, so this
            // is 1234.50 -- not 123450. Asserted because it is the *other*
            // direction of the same hazard that matters, and because a reader
            // that dropped InvariantCulture would turn it into 123450, a 100x
            // error in a price, arrived at by being helpful.
            Read(@"{""score"":""1,234.50""}").Should().Be(1234.50m);
        }

        [Fact]
        public void A_grouped_value_is_unaffected_by_the_ambient_culture()
        {
            // The real point of passing InvariantCulture. Under a comma-decimal
            // culture the same string means something different, so a reader that
            // used the ambient culture would disagree with this one by two orders
            // of magnitude -- and only on the machines of users who live there.
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Read(@"{""score"":""1,234.50""}").Should().Be(1234.50m);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void A_dot_decimal_value_is_unaffected_by_the_ambient_culture()
        {
            // "62.52" means 62.52 in the invariant culture this reader uses.
            // Under de-DE the same characters are read as 6252 by a
            // culture-sensitive parser, which is why the culture is pinned here.
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Read("""{"score":62.5231}""").Should().Be(62.5231m);
                Read(@"{""score"":""62.52""}").Should().Be(62.52m);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }

    public class ARoundTrip
    {
        [Theory]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("62.52")]
        [InlineData("1000000.99")]
        [InlineData("-17.25")]
        public void What_we_write_we_can_read_again(string raw)
        {
            // Write emits two decimal places, so the round trip is asserted at
            // that precision rather than pretending the fourth digit survives.
            var original = decimal.Parse(raw, CultureInfo.InvariantCulture);

            Read(Write(original)).Should().Be(original);
        }
    }

    public class ATokenThatIsNeither
    {
        [Fact]
        public void Null_is_refused_with_a_message_naming_the_token()
        {
            // A silent 0 here would put a fabricated score in the database.
            var act = () => Read("""{"score":null}""");
            act.Should().Throw<JsonException>()
                .WithMessage("*JSON Null*");
        }

        [Fact]
        public void A_boolean_is_refused()
        {
            var act = () => Read("""{"score":true}""");
            act.Should().Throw<JsonException>()
                .WithMessage("*JSON True*");
        }

        [Fact]
        public void An_array_is_refused()
        {
            var act = () => Read("""{"score":[1]}""");
            act.Should().Throw<JsonException>()
                .WithMessage("*JSON StartArray*");
        }
    }

    /// <summary>
    /// Through the real DTO the agent service binds to.
    /// </summary>
    /// <remarks>
    /// The unit tests above pin the converter. These pin that the converter is
    /// actually the one in force for this DTO -- the global registration in
    /// <c>Program.cs</c> is exactly the kind of wiring that can be removed without
    /// any test noticing, and removing it is what made the bug invisible in the
    /// first place: with default options a bare number binds fine.
    /// </remarks>
    public class AgainstTheAgentRequestDto
    {
        private static readonly JsonSerializerOptions AgentOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
                new DecimalAsStringJsonConverter(),
                new IsoDateTimeJsonConverter(),
            },
        };

        private const string FreelancerGuid = "09faf944-505c-4ae7-9a89-a78261d7d85e";

        /// <summary>
        /// The body from run 24, field for field, as the client built it.
        /// </summary>
        /// <remarks>
        /// Built with escapes rather than a verbatim literal, because
        /// <c>factors_json</c> is a JSON document inside a JSON string and needs
        /// its own layer of quoting. Written verbatim it produces
        /// <c>""skill_coverage""</c> inside the string value -- unescaped quotes,
        /// invalid JSON, and a parse error that looks nothing like the binding
        /// failure under test.
        /// </remarks>
        private const string AgentBody =
            "{\"project_id\":11,\"freelancer_id\":\"09faf944-505c-4ae7-9a89-a78261d7d85e\","
            + "\"score\":62.5231,\"factors_json\":\"{\\\"skill_coverage\\\":40.0}\","
            + "\"reason\":\"Decent fit\",\"strengths\":[\"React\"],\"gaps\":[],"
            + "\"portfolio_samples_json\":\"[]\"}";

        [Fact]
        public void The_bare_body_the_agent_service_sends_now_binds()
        {
            var act = () => JsonSerializer.Deserialize<AgentRecommendationRequest>(
                AgentBody, AgentOptions);

            act.Should().NotThrow(
                "this exact payload was rejected four times in a row on run 24");
        }

        [Fact]
        public void And_the_score_survives_intact()
        {
            var dto = JsonSerializer.Deserialize<AgentRecommendationRequest>(
                AgentBody, AgentOptions)!;

            dto.Score.Should().Be(62.5231m);
            dto.ProjectId.Should().Be(11);
            dto.FreelancerId.Should().Be(Guid.Parse(FreelancerGuid));
            dto.Strengths.Should().ContainSingle().Which.Should().Be("React");
        }
    }

    /// <summary>
    /// Every other internal write that carries a decimal.
    /// </summary>
    /// <remarks>
    /// Not hypothetical. These are the DTOs the agent service PUTs to, and each
    /// one hit the same wall; the shortlist was simply the first to be noticed
    /// because it is the only one whose emptiness is visible on a screen.
    /// </remarks>
    public class EveryDecimalTheAgentsWrite
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new DecimalAsStringJsonConverter() },
        };

        [Fact]
        public void A_screening_fit_score_binds_from_a_bare_number()
        {
            var json = @"{""proposal_id"":7,""fit_score"":88.25,""strengths_json"":""[]"","
                + @"""concerns_json"":""[]"",""recommendation"":""accept"",""rationale"":""Good fit""}";

            var act = () => JsonSerializer.Deserialize<ProposalScreeningRequest>(json, Options);
            act.Should().NotThrow();
        }
    }
}
