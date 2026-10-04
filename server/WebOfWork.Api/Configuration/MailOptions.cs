namespace WebOfWork.Api.Configuration;

/// <summary>
/// SMTP settings for the MailKit sender.
/// Host: mail.xetuwiz.de:587 with STARTTLS (from application.properties + plan spec).
/// Do NOT use Gmail SMTP — that was a the reference legacy error.
/// </summary>
public sealed class MailOptions
{
    public const string Section = "Mail";

    public string Host        { get; init; } = "mail.xetuwiz.de";
    public int    Port        { get; init; } = 587;
    public bool   UseSsl      { get; init; } = false;
    public bool   StartTls    { get; init; } = true;
    public string Username    { get; init; } = "no-reply@xetuwiz.de";
    public string Password    { get; init; } = string.Empty;
    public string FromAddress { get; init; } = "no-reply@xetuwiz.de";
    public string FromName    { get; init; } = "WebOfWork";
}
