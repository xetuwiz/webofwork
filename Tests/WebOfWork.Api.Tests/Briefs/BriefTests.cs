using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebOfWork.Api.Controllers;
using WebOfWork.Api.DTOs.Briefs;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Tests.Briefs;

public class BriefTests
{
    [Fact]
    public async Task GetBriefDetailAsync_ShouldReturnForbid_WhenNotOwner()
    {
        // Arrange
        var mockService = new Mock<IBriefService>();
        var mockCurrentUser = new Mock<ICurrentUser>();
        
        var clientId = Guid.NewGuid();
        mockCurrentUser.Setup(s => s.Id).Returns(clientId);

        mockService.Setup(s => s.GetBriefDetailAsync(clientId, 1))
            .ThrowsAsync(new UnauthorizedAccessException("You do not have permission to access this brief."));

        var controller = new BriefsController(mockService.Object, mockCurrentUser.Object);

        // Act
        var result = await controller.GetBrief(1);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }
}
