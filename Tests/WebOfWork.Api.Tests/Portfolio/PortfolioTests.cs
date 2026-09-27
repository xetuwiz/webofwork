using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using WebOfWork.Api.DTOs.Portfolio;
using WebOfWork.Api.Validators.Portfolio;

namespace WebOfWork.Api.Tests.Portfolio;

public class PortfolioTests
{
    [Fact]
    public async Task UploadPortfolioMediaValidator_ShouldFail_WhenSizeExceeds10MB()
    {
        var validator = new UploadPortfolioMediaValidator();
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(11 * 1024 * 1024); // 11 MB
        mockFile.Setup(f => f.ContentType).Returns("image/jpeg");

        var request = new UploadPortfolioMediaRequest
        {
            PortfolioItemId = Guid.NewGuid(),
            File = mockFile.Object
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("10 MB"));
    }

    [Fact]
    public async Task UploadPortfolioMediaValidator_ShouldFail_WhenMimeInvalid()
    {
        var validator = new UploadPortfolioMediaValidator();
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(1 * 1024 * 1024);
        mockFile.Setup(f => f.ContentType).Returns("application/zip"); // Invalid

        var request = new UploadPortfolioMediaRequest
        {
            PortfolioItemId = Guid.NewGuid(),
            File = mockFile.Object
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Invalid file type"));
    }
}
