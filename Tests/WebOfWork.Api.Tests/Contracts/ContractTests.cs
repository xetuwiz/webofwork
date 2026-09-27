using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebOfWork.Api.Controllers;
using WebOfWork.Api.DTOs.Contracts;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Tests.Contracts;

public class ContractTests
{
    private AcceptContractRequest CreateValidAcceptRequest() => new()
    {
        Signature = "John Doe",
        SignatureType = "type"
    };

    [Fact]
    public async Task GetContractDetail_ShouldReturn403_WhenUserNotParticipant()
    {
        var mockService = new Mock<IContractService>();
        var mockCurrentUser = new Mock<ICurrentUser>();
        var userId = Guid.NewGuid();

        mockCurrentUser.Setup(u => u.Id).Returns(userId);
        mockService
            .Setup(s => s.GetContractDetailAsync(userId, 1))
            .ThrowsAsync(new UnauthorizedAccessException("You are not a participant in this contract."));

        var controller = new ContractsController(mockService.Object, mockCurrentUser.Object);
        var result = await controller.GetContractDetail(1);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task AcceptContract_ShouldReturn403_WhenUserNotFreelancer()
    {
        var mockService = new Mock<IContractService>();
        var mockCurrentUser = new Mock<ICurrentUser>();
        var userId = Guid.NewGuid();

        mockCurrentUser.Setup(u => u.Id).Returns(userId);
        
        var controller = new ContractsController(mockService.Object, mockCurrentUser.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };

        mockService
            .Setup(s => s.AcceptContractAsync(userId, 1, It.IsAny<AcceptContractRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new UnauthorizedAccessException("Only the assigned freelancer can accept this contract."));

        var result = await controller.AcceptContract(1, CreateValidAcceptRequest());

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task UpdateStatus_ShouldReturn204_WhenPayoutComputedOnCompletion()
    {
        var mockService = new Mock<IContractService>();
        var mockCurrentUser = new Mock<ICurrentUser>();
        var userId = Guid.NewGuid();

        mockCurrentUser.Setup(u => u.Id).Returns(userId);
        // Status is a typed `ContractStatus` on both the request DTO and the
        // service, rather than a string the controller re-parsed.
        mockService
            .Setup(s => s.UpdateStatusAsync(userId, 1, ContractStatus.Completed))
            .ReturnsAsync(true);

        var controller = new ContractsController(mockService.Object, mockCurrentUser.Object);
        var result = await controller.UpdateStatus(1, new UpdateContractStatusRequest { Status = ContractStatus.Completed });

        result.Should().BeOfType<NoContentResult>();
    }
}
