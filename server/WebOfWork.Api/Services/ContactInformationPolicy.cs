using System.Text.RegularExpressions;

namespace WebOfWork.Api.Services;

public interface IContactInformationPolicy
{
    bool ContainsContactInfo(string text);
    IReadOnlyList<string> DetectPatterns(string text);
}

public partial class ContactInformationPolicy : IContactInformationPolicy
{
    public bool ContainsContactInfo(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return DetectPatterns(text).Count > 0;
    }

    public IReadOnlyList<string> DetectPatterns(string text)
    {
        var hits = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return hits;

        if (PhoneRegex().IsMatch(text)) hits.Add("phone_number");
        if (EmailRegex().IsMatch(text)) hits.Add("email_address");
        if (SocialKeywordRegex().IsMatch(text)) hits.Add("social_media_keyword");
        if (DomainRegex().IsMatch(text)) hits.Add("domain_or_url");

        return hits;
    }

    [GeneratedRegex(@"(\+?\d{1,4}[-.\s]?\(?\d{1,3}\)?[-.\s]?\d{1,4}[-.\s]?\d{1,4}[-.\s]?\d{1,9})", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(whatsapp|telegram|instagram|insta|facebook|fb|skype|number|email|phn|num|wtsp|tele|linkedin|viber|discord)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex SocialKeywordRegex();

    [GeneratedRegex(@"(([a-z0-9]+\.)*[a-z0-9-]+\.[a-z]{2,}(?:/[^\s]*)?)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex DomainRegex();
}
