using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebOfWork.Api;

/// <summary>
/// Serialises a <see cref="decimal"/> as a JSON string, and reads one back.
/// </summary>
/// <remarks>
/// <para>
/// The string form is deliberate and stays. It is what the Flutter app and the
/// React client already parse, and changing the write side would silently turn
/// every price and score in every response into a different JavaScript type.
/// </para>
/// <para>
/// <b>Reading a bare JSON number is also accepted, and has to be.</b>
/// Previously <c>Read</c> called <c>reader.GetString()</c> unconditionally.
/// That works for a string token and throws for a number token -- and a decimal's
/// native JSON representation <i>is</i> a number, so the only payloads this
/// converter could read were the ones it had itself written.
/// </para>
/// <para>
/// Consequence, observed live: the Python agent service sends
/// <c>{"score": 62.5231}</c>, which is the correct and obvious thing to send, and
/// every write was rejected with
/// <c>$.score: The JSON value could not be converted to System.Decimal</c>. Four
/// recommendations in one run, so the shortlist rendered empty while the run
/// reported success. Other internal writes carrying a decimal -- a screening's
/// <c>fit_score</c> among them -- fail the same way.
/// </para>
/// <para>
/// The asymmetry is inherent to the format, not a quirk of this converter: we
/// choose to <i>write</i> decimals as strings, which obliges us to <i>read</i> both
/// forms. Rejecting the native one only moves the error somewhere less obvious.
/// </para>
/// </remarks>
public sealed class DecimalAsStringJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            // The form we write. Invariant culture, because "1,234.50" under a
            // comma-decimal culture parses as 123450 and silently corrupts money.
            JsonTokenType.String => decimal.Parse(
                reader.GetString()!, CultureInfo.InvariantCulture),

            // The form everybody else writes, including every model provider
            // and the Python service. `GetDecimal` parses the raw token straight
            // to decimal, so nothing round-trips through a double on the way.
            JsonTokenType.Number => reader.GetDecimal(),

            _ => throw new JsonException(
                $"Cannot read a decimal from a JSON {reader.TokenType} token."),
        };

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString("0.00", CultureInfo.InvariantCulture));
}

public sealed class IsoDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => DateTime.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
}
