using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebOfWork.Api.Configuration;
using WebOfWork.Api.Integrations.Sms;

namespace WebOfWork.Api.Tests.Sms;

public class AndroidSmsGatewaySmsSenderTests
{
    [Fact]
    public async Task SendSmsAsync_ShouldFormatPhoneAndPostCorrectly()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };
        var sender = new AndroidSmsGatewaySmsSender(
            client,
            new NullLogger<AndroidSmsGatewaySmsSender>(),
            Options.Create(new AndroidSmsGatewayOptions()));

        await sender.SendSmsAsync("0771234567", "Test");

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task SendSmsAsync_RetriesAfterServerError()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("server error")
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("ok")
            });

        var client = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };
        var sender = new AndroidSmsGatewaySmsSender(
            client,
            new NullLogger<AndroidSmsGatewaySmsSender>(),
            Options.Create(new AndroidSmsGatewayOptions()));

        await sender.SendSmsAsync("0779091086", "Test retry");

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
            ItExpr.IsAny<CancellationToken>());
    }

    [Theory]
    [InlineData("0779091086", "+94779091086")]
    [InlineData("779091086", "+94779091086")]
    [InlineData("+94 77 909 1086", "+94779091086")]
    [InlineData("0094779091086", "+94779091086")]
    public void NormalizeSriLankanPhoneNumber_AcceptsStandardLocalForms(string input, string expected)
    {
        AndroidSmsGatewaySmsSender.NormalizeSriLankanPhoneNumber(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("+15551234567")]
    [InlineData("0112345678")]
    [InlineData("071234567890")]
    public void NormalizeSriLankanPhoneNumber_RejectsInvalidOrNonSriLankanNumbers(string input)
    {
        Action act = () => AndroidSmsGatewaySmsSender.NormalizeSriLankanPhoneNumber(input);
        act.Should().Throw<ArgumentException>().WithMessage("*Sri Lankan mobile number*");
    }

}
