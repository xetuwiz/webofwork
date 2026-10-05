using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebOfWork.Api.Configuration;
using WebOfWork.Api.Integrations.PayPal;
using Xunit;

namespace WebOfWork.Api.Tests.Refunds;

public class PayPalRefundClientTests
{
    [Fact]
    public async Task RefundOrderAsync_UsesUsdPayPalCurrency()
    {
        const string orderJson = """
            {
              "purchase_units": [
                {
                  "payments": {
                    "captures": [{ "id": "CAPTURE-123" }]
                  }
                }
              ]
            }
            """;

        var handler = new CapturingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"token"}""")
            },
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(orderJson)
            },
            _ => new HttpResponseMessage(HttpStatusCode.Created));

        var client = new PayPalRefundClient(
            new HttpClient(handler),
            Options.Create(new PayPalOptions { ClientId = "id", Secret = "secret", Currency = "USD" }),
            NullLogger<PayPalRefundClient>.Instance);

        var refunded = await client.RefundOrderAsync("ORDER-1", 100m, "test refund");

        refunded.Should().BeTrue();
        handler.Bodies.Should().Contain(body =>
            body.Contains("\"currency_code\":\"USD\"") && body.Contains("\"value\":\"100.00\""));
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
