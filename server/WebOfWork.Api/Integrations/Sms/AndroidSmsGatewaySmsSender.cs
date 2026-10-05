using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebOfWork.Api.Configuration;

namespace WebOfWork.Api.Integrations.Sms;

/// <summary>
/// Single source of truth for Sri Lankan mobile numbers.
/// <para>
/// Accepts every shape a user realistically types and returns canonical E.164
/// (<c>+947XXXXXXXX</c>). All of these normalize to the same value:
/// <c>+94779091086</c>, <c>94779091086</c>, <c>0094779091086</c>,
/// <c>0779091086</c>, <c>077 909 1086</c>, <c>077-909-1086</c>,
/// <c>(077) 909 1086</c>, <c>779091086</c>.
/// </para>
/// <para>
/// Only the allocated mobile ranges are accepted. Landline (0 11/12/...),
/// 070-072/074-078 only. In particular 073 and 079 are not allocated and are
/// rejected, as is anything that is not 9 national digits starting with 7.
/// </para>
/// </summary>
public static class SriLankanPhone
{
    public const string CountryCode = "94";
    public const string MobilePrefix = "7";
    public const int NationalNumberLength = 9;      // 7XXXXXXXX
    public const int LocalNumberLength = 10;        // 07XXXXXXXX
    public const string ErrorMessage = "Enter a valid Sri Lankan mobile number, e.g. 0779091086 or +94779091086.";

    // Allocated mobile operator codes (national format, 07X).
    private static readonly string[] AllocatedOperators =
    {
        "070", "071", "072", "074", "075", "076", "077", "078"
    };

    /// <summary>
    /// Converts any accepted Sri Lankan mobile format to <c>+947XXXXXXXX</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The number is not a valid SL mobile.</exception>
    public static string Normalize(string? phoneNumber)
    {
        if (!TryNormalize(phoneNumber, out var normalized, out var error))
        {
            throw new ArgumentException(error, nameof(phoneNumber));
        }

        return normalized!;
    }

    /// <summary>
    /// Non-throwing counterpart to <see cref="Normalize"/>.
    /// </summary>
    public static bool TryNormalize(string? phoneNumber, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;

        // Strip everything that is not a digit: spaces, dashes, dots, brackets,
        // and a leading '+'. "077 909-1086" and "(077) 909 1086" both collapse
        // to the same 10 digits.
        var digits = new string((phoneNumber ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

        // International access code.
        if (digits.StartsWith("00" + CountryCode, StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        // Drop a bare country code (94XXXXXXXXX).
        if (digits.Length == NationalNumberLength + 2 && digits.StartsWith(CountryCode, StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        // Drop a leading trunk zero (0 7 XXXXXXX -> 7 XXXXXXX).
        if (digits.Length == LocalNumberLength && digits[0] == '0')
        {
            digits = digits[1..];
        }

        if (digits.Length != NationalNumberLength ||
            !digits.StartsWith(MobilePrefix, StringComparison.Ordinal))
        {
            error = ErrorMessage;
            return false;
        }

        // Reject unallocated operator codes (073, 079, ...).
        // digits[0] is the mobile marker (7); digits[1] is the operator digit.
        var operatorCode = "0" + digits[..2];
        if (Array.IndexOf(AllocatedOperators, operatorCode) < 0)
        {
            error = $"Mobile prefix {operatorCode} is not an allocated Sri Lankan operator. Use 070, 071, 072, 074, 075, 076, 077 or 078.";
            return false;
        }

        normalized = "+" + CountryCode + digits;
        return true;
    }

    /// <summary>Convenience predicate for validators.</summary>
    public static bool IsValid(string? phoneNumber) => TryNormalize(phoneNumber, out _, out _);
}

public class AndroidSmsGatewaySmsSender : ISmsSender
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AndroidSmsGatewaySmsSender> _logger;
    private readonly AndroidSmsGatewayOptions _options;

    public AndroidSmsGatewaySmsSender(
        HttpClient httpClient,
        ILogger<AndroidSmsGatewaySmsSender> logger,
        IOptions<AndroidSmsGatewayOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task SendSmsAsync(string phoneNumber, string message)
    {
        var normalizedPhone = NormalizeSriLankanPhoneNumber(phoneNumber);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var payload = new
                {
                    textMessage = new { text = message },
                    simNumber = _options.SimNumber,
                    phoneNumbers = new[] { normalizedPhone }
                };
                var response = await _httpClient.PostAsJsonAsync("messages", payload);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("SMS sent successfully to {Phone} on attempt {Attempt}", normalizedPhone, attempt);
                    return;
                }

                if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
                {
                    _logger.LogWarning(
                        "Android SMS gateway returned {StatusCode} for {Phone} on attempt {Attempt}; retrying.",
                        response.StatusCode,
                        normalizedPhone,
                        attempt);
                    if (attempt < 3)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
                        continue;
                    }
                }

                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                if (attempt < 3)
                {
                    _logger.LogWarning(ex, "Temporary SMS send failure to {Phone} on attempt {Attempt}; retrying.", normalizedPhone, attempt);
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
                    continue;
                }

                _logger.LogError(ex, "Failed to send SMS via Android gateway to {Phone} after retries", normalizedPhone);
                throw;
            }
        }
    }

    /// <summary>
    /// Kept for existing callers. Delegates to <see cref="SriLankanPhone"/>
    /// so the codebase has exactly one set of phone rules.
    /// </summary>
    public static string NormalizeSriLankanPhoneNumber(string phoneNumber)
        => SriLankanPhone.Normalize(phoneNumber);
}
