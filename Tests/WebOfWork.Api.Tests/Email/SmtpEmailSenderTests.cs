using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using WebOfWork.Api.Integrations.Email;
using WebOfWork.Api.Configuration;
using Microsoft.Extensions.Options;

namespace WebOfWork.Api.Tests.Email;

public class SmtpEmailSenderTests
{
    [Fact] // Uncomment to manually test against real SMTP server
    public async Task SendAsync_ShouldSendEmail_WhenCredentialsAreValid()
    {
        // Arrange
        var options = Options.Create(new MailOptions {
            Host = "mail.xetuwiz.de",
            Port = 587,
            Username = "no-reply@xetuwiz.de",
            Password = "Noreplypassword1#",
            FromAddress = "no-reply@xetuwiz.de",
            FromName = "WebOfWork"
        });
        
        var sender = new SmtpEmailSender(options, NullLogger<SmtpEmailSender>.Instance);
        var testEmail = $"test+{Guid.NewGuid()}@xetuwiz.de";
        
        // Act
        var ex = await Record.ExceptionAsync(() => 
            sender.SendAsync(testEmail, "Integration Test", "<h1>Test</h1>"));
            
        // Assert
        Assert.Null(ex);
    }
}
