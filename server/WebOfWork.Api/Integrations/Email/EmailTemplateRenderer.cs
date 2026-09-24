using Microsoft.Extensions.Hosting;

namespace WebOfWork.Api.Integrations.Email;

public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync(string templateName, IReadOnlyDictionary<string, string> values, CancellationToken ct = default);
}

public sealed class EmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly IHostEnvironment _environment;
    public EmailTemplateRenderer(IHostEnvironment environment) => _environment = environment;

    public async Task<string> RenderAsync(string templateName, IReadOnlyDictionary<string, string> values, CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(templateName);
        var path = Path.Combine(_environment.ContentRootPath, "Templates", "Email", safeName);
        if (!File.Exists(path)) throw new FileNotFoundException("Email template not found.", path);
        var html = await File.ReadAllTextAsync(path, ct);
        foreach (var pair in values) html = html.Replace($"@Model.{pair.Key}", System.Net.WebUtility.HtmlEncode(pair.Value));
        return html;
    }
}
