using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebOfWork.Api.Configuration;
using WebOfWork.Api.Integrations.PayPal;
using Xunit;
using FluentAssertions;

namespace WebOfWork.Api.Tests.Payouts;

public class PayPalPayoutClientTests
{
    [Fact] // 13.2
    public async Task SendPayoutAsync_UsesUsdPayPalCurrency()
    {
        var handler = new CapturingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"token"}""")
            },
            _ => new HttpResponseMessage(HttpStatusCode.Created));
        var client = new PayPalPayoutClient(
            new HttpClient(handler),
            Options.Create(new PayPalOptions { ClientId = "id", Secret = "secret", Currency = "USD" }),
            NullLogger<PayPalPayoutClient>.Instance);

        var sent = await client.SendPayoutAsync("freelancer@example.com", 90m, "test payout");

        sent.Should().BeTrue();
        handler.Bodies.Should().Contain(body => body.Contains("\"currency\":\"USD\""));
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
