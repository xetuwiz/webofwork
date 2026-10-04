using System.Text.RegularExpressions;

namespace WebOfWork.Api.Services;

public static partial class CommunicationContactValidator
{
    [GeneratedRegex(@"(\+?\d{1,4}?[-.\s]?\(?\d{1,3}?\)?[-.\s]?\d{1,4}[-.\s]?\d{1,4}[-.\s]?\d{1,9})", RegexOptions.IgnoreCase)]
    private static partial Regex PhonePattern();
    [GeneratedRegex(@"[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+", RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();
    [GeneratedRegex(@"(whatsapp|telegram|instagram|insta|facebook|skype|number|email|phn|num|wtsp|tele|linkedin|http|https|@)", RegexOptions.IgnoreCase)]
    private static partial Regex SocialPattern();
    [GeneratedRegex(@"(([a-z0-9]+\.)*[a-z0-9]+\.[a-z]{2,}(?:\/[^\s]*)?)", RegexOptions.IgnoreCase)]
    private static partial Regex LinkPattern();
    public static bool ContainsContactInfo(string? text) => !string.IsNullOrEmpty(text) && (PhonePattern().IsMatch(text) || EmailPattern().IsMatch(text) || SocialPattern().IsMatch(text) || LinkPattern().IsMatch(text));
}
