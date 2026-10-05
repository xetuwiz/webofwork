using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebOfWork.Api.Configuration;
using WebOfWork.Api.Integrations.PayPal;
using Xunit;

namespace WebOfWork.Api.Tests.Payments;

public class PayPalWebhookVerifierTests
{
    [Fact]
    public async Task VerifyAsync_PostsSignatureVerificationPayloadWithConfiguredWebhookId()
    {
        var handler = new CapturingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"token"}""")
            },
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"verification_status":"VERIFIED"}""")
            });
        var verifier = new PayPalWebhookVerifier(
            new HttpClient(handler),
            Options.Create(new PayPalOptions
            {
                ClientId = "id",
                Secret = "secret",
                WebhookId = "WH-123",
                Mode = "sandbox"
            }),
            NullLogger<PayPalWebhookVerifier>.Instance);
        var headers = new HeaderDictionary
        {
            ["PAYPAL-AUTH-ALGO"] = "SHA256withRSA",
            ["PAYPAL-CERT-URL"] = "https://api.sandbox.paypal.com/cert.pem",
            ["PAYPAL-TRANSMISSION-ID"] = "transmission-id",
            ["PAYPAL-TRANSMISSION-SIG"] = "signature",
            ["PAYPAL-TRANSMISSION-TIME"] = "2026-09-21T10:00:00Z"
        };

        var verified = await verifier.VerifyAsync(headers, """{"id":"event-1","event_type":"CHECKOUT.ORDER.APPROVED"}""", CancellationToken.None);

        verified.Should().BeTrue();
        handler.Bodies.Should().Contain(body => body.Contains("\"webhook_id\":\"WH-123\""));
        handler.Bodies.Should().Contain(body => body.Contains("\"webhook_event\":{\"id\":\"event-1\""));
    }

    [Fact]
    public async Task VerifyAsync_ReturnsFalse_WhenPaypalRejectsSignature()
    {
        var handler = new CapturingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"token"}""")
            },
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"verification_status":"FAILURE"}""")
            });
        var verifier = new PayPalWebhookVerifier(
            new HttpClient(handler),
            Options.Create(new PayPalOptions { ClientId = "id", Secret = "secret", WebhookId = "WH-123" }),
            NullLogger<PayPalWebhookVerifier>.Instance);

        var verified = await verifier.VerifyAsync(new HeaderDictionary(), """{"id":"event-1"}""", CancellationToken.None);

        verified.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_AcceptsSuccessStatus_RealPayPalSandboxResponse()
    {
        var handler = new CapturingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"token"}""")
            },
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"verification_status":"SUCCESS"}""")
            });
        var verifier = new PayPalWebhookVerifier(
            new HttpClient(handler),
            Options.Create(new PayPalOptions { ClientId = "id", Secret = "secret", WebhookId = "87067635YL5310459" }),
            NullLogger<PayPalWebhookVerifier>.Instance);
        var headers = new HeaderDictionary
        {
            ["PAYPAL-AUTH-ALGO"] = "SHA256withRSA",
            ["PAYPAL-CERT-URL"] = "https://api.sandbox.paypal.com/v1/notifications/certs/CERT-360caa42-fca2a594-bf9ab596",
            ["PAYPAL-TRANSMISSION-ID"] = "43134bbc-b5b2-11f1-b7db-c726fd309b1a",
            ["PAYPAL-TRANSMISSION-SIG"] = "sig",
            ["PAYPAL-TRANSMISSION-TIME"] = "2026-09-21T11:47:45Z"
        };

        var verified = await verifier.VerifyAsync(headers, """{"id":"evt-real"}""", CancellationToken.None);

        verified.Should().BeTrue();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses;

        public List<string> Bodies { get; } = [];

        public CapturingHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
        {
            _responses = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return _responses.Dequeue()(request);
        }
    }
}
