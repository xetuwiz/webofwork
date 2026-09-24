using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Services;

public interface IContractDocumentService
{
    Task<byte[]> GenerateContractPdfAsync(Contract contract);
}

public class ContractDocumentService : IContractDocumentService
{
    private static readonly HttpClient HttpClient = new();

    public ContractDocumentService()
    {
        // QuestPDF requires setting the license type for commercial/community use
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerateContractPdfAsync(Contract contract)
    {
        var clientSignature = await DownloadSignatureAsync(contract.ClientSignature, CancellationToken.None);
        var freelancerSignature = await DownloadSignatureAsync(contract.FreelancerSignature, CancellationToken.None);
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Element(ComposeHeader);
                page.Content().Element(c => ComposeContent(c, contract, clientSignature, freelancerSignature));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row => row.RelativeItem().Column(column => column.Item()
            .Text("WebOfWork - Independent Contractor Agreement")
            .FontSize(18).SemiBold().FontColor(Colors.Blue.Darken2)));
    }

    private static void ComposeContent(IContainer container, Contract contract, byte[]? clientSignature, byte[]? freelancerSignature)
    {
        container.PaddingVertical(1, Unit.Centimetre).Column(column =>
        {
            column.Spacing(20);
            column.Item().Text(text => { text.Span("Project: ").SemiBold(); text.Span(contract.Project?.Title ?? "N/A"); });
            column.Item().Text(text => { text.Span("Client: ").SemiBold(); text.Span(contract.Client?.User?.FullName ?? "N/A"); });
            column.Item().Text(text => { text.Span("Freelancer: ").SemiBold(); text.Span(contract.Freelancer?.User?.FullName ?? "N/A"); });
            column.Item().Text(text => { text.Span("Total Amount: ").SemiBold(); text.Span($"USD {contract.TotalAmount:N2}"); });
            column.Item().Text("Terms & Conditions").FontSize(14).SemiBold();
            column.Item().Text("This agreement constitutes a binding contract between the Client and Freelancer. The Freelancer agrees to perform the work as described in the Project brief. The Client agrees to pay the Total Amount upon successful completion of the work.");
            column.Item().PaddingTop(25).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Client Signature:").SemiBold();
                    col.Item().Element(c => ComposeSignature(c, contract.ClientSignature, clientSignature));
                    col.Item().Text($"Date: {contract.ClientSignedAt:yyyy-MM-dd}");
                    col.Item().Text($"IP: {contract.ClientIp ?? "N/A"}");
                });
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Freelancer Signature:").SemiBold();
                    col.Item().Element(c => ComposeSignature(c, contract.FreelancerSignature, freelancerSignature));
                    col.Item().Text($"Date: {(contract.FreelancerSignedAt.HasValue ? contract.FreelancerSignedAt.Value.ToString("yyyy-MM-dd") : "Pending")}");
                    col.Item().Text($"IP: {contract.FreelancerIp ?? "N/A"}");
                });
            });
        });
    }

    private static void ComposeSignature(IContainer container, string? signature, byte[]? imageBytes)
    {
        if (imageBytes is { Length: > 0 and <= 5_000_000 }) container.Image(imageBytes).FitArea();
        else if (string.IsNullOrWhiteSpace(signature)) container.Text("Pending");
        else container.Text(signature);
    }

    private static async Task<byte[]?> DownloadSignatureAsync(string? signature, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(signature) || !Uri.TryCreate(signature, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            return await HttpClient.GetByteArrayAsync(uri, timeout.Token);
        }
        catch (Exception) { return null; }
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(x =>
        {
            x.Span("Page ");
            x.CurrentPageNumber();
            x.Span(" of ");
            x.TotalPages();
        });
    }
}
