using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using WebOfWork.Api.DTOs.Invitations;
using WebOfWork.Api.Validators.Invitations;
using WebOfWork.Api.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebOfWork.Api.Controllers;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Tests.Invitations;

public class InvitationTests
{
    private readonly CreateInvitationValidator _validator = new();

    private CreateInvitationRequest CreateValidInvitationRequest() => new()
    {
        FreelancerId = Guid.NewGuid(),
        ProjectId    = 1,
        PackageId    = Guid.NewGuid(),
        Message      = "We think you'd be a great fit for this project."
    };

    private InvitationResponse BuildFakeResponse(int id) => new()
    {
        Id          = id,
        ClientId    = Guid.NewGuid(),
        FreelancerId = Guid.NewGuid(),
        ProjectId   = 1,
        PackageId   = Guid.NewGuid(),
        // `InvitationResponse.Status` is the enum itself now, so the client sees
        // the wire name from a JSON converter rather than a string the DTO held.
        Status      = InvitationStatus.Pending,
        CreatedAt   = DateTime.UtcNow
    };

    // 8.25 — Duplicate invitation → 400
    [Fact]
    public async Task CreateInvitation_ShouldReturn400_WhenAlreadyInvited()
    {
        var mockService     = new Mock<IInvitationService>();
        var mockCurrentUser = new Mock<ICurrentUser>();
        var mockValidator   = new Mock<IValidator<CreateInvitationRequest>>();
        var userId          = Guid.NewGuid();

        mockCurrentUser.Setup(u => u.Id).Returns(userId);
        mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateInvitationRequest>(), default))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());
        mockService
            .Setup(s => s.CreateInvitationAsync(userId, It.IsAny<CreateInvitationRequest>()))
            .ThrowsAsync(new InvalidOperationException("You have already invited this freelancer to this project."));

        var controller = new InvitationsController(mockService.Object, mockCurrentUser.Object);
        var result     = await controller.CreateInvitation(CreateValidInvitationRequest(), mockValidator.Object);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // 8.26 — Invitation when proposal already exists → 400
    [Fact]
    public async Task CreateInvitation_ShouldReturn400_WhenProposalAlreadyExists()
    {
        var mockService     = new Mock<IInvitationService>();
        var mockCurrentUser = new Mock<ICurrentUser>();
        var mockValidator   = new Mock<IValidator<CreateInvitationRequest>>();
        var userId          = Guid.NewGuid();

        mockCurrentUser.Setup(u => u.Id).Returns(userId);
        mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateInvitationRequest>(), default))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());
        mockService
            .Setup(s => s.CreateInvitationAsync(userId, It.IsAny<CreateInvitationRequest>()))
            .ThrowsAsync(new InvalidOperationException("Freelancer has already submitted a proposal for this project."));

        var controller = new InvitationsController(mockService.Object, mockCurrentUser.Object);
        var result     = await controller.CreateInvitation(CreateValidInvitationRequest(), mockValidator.Object);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // 8.27 — Freelancer can accept an invitation
    [Fact]
    public async Task UpdateInvitationStatus_ShouldReturn204_WhenFreelancerAccepts()
    {
        var mockService     = new Mock<IInvitationService>();
        var mockCurrentUser = new Mock<ICurrentUser>();
        var userId          = Guid.NewGuid();

        mockCurrentUser.Setup(u => u.Id).Returns(userId);
        mockService
            .Setup(s => s.UpdateInvitationStatusAsync(userId, 1, It.IsAny<UpdateInvitationStatusRequest>()))
            .ReturnsAsync(true);

        var controller = new InvitationsController(mockService.Object, mockCurrentUser.Object);
        var result     = await controller.UpdateInvitationStatus(1, new UpdateInvitationStatusRequest { Status = InvitationStatus.Accepted });

        result.Should().BeOfType<NoContentResult>();
    }

    // 8.27 — Freelancer can decline an invitation
    [Fact]
    public async Task UpdateInvitationStatus_ShouldReturn204_WhenFreelancerDeclines()
    {
        var mockService     = new Mock<IInvitationService>();
        var mockCurrentUser = new Mock<ICurrentUser>();
        var userId          = Guid.NewGuid();

        mockCurrentUser.Setup(u => u.Id).Returns(userId);
        mockService
            .Setup(s => s.UpdateInvitationStatusAsync(userId, 1, It.IsAny<UpdateInvitationStatusRequest>()))
            .ReturnsAsync(true);

        var controller = new InvitationsController(mockService.Object, mockCurrentUser.Object);
        var result     = await controller.UpdateInvitationStatus(1, new UpdateInvitationStatusRequest { Status = InvitationStatus.Declined });

        result.Should().BeOfType<NoContentResult>();
    }

    // 8.22b — Only client can cancel invitation
    [Fact]
    public async Task CancelInvitation_ShouldReturn403_WhenCallerNotOwner()
    {
        var mockService     = new Mock<IInvitationService>();
        var mockCurrentUser = new Mock<ICurrentUser>();
        var userId          = Guid.NewGuid();

        mockCurrentUser.Setup(u => u.Id).Returns(userId);
        mockService
            .Setup(s => s.CancelInvitationAsync(userId, 1))
            .ThrowsAsync(new UnauthorizedAccessException("You do not own this invitation."));

        var controller = new InvitationsController(mockService.Object, mockCurrentUser.Object);
        var result     = await controller.CancelInvitation(1);

        result.Should().BeOfType<ForbidResult>();
    }

    // 8.22b — Freelancer cannot cancel (sends Cancelled status via PATCH /status)
    [Fact]
    public async Task UpdateInvitationStatus_ShouldReturn400_WhenFreelancerTriesToCancel()
    {
        var mockService     = new Mock<IInvitationService>();
        var mockCurrentUser = new Mock<ICurrentUser>();
        var userId          = Guid.NewGuid();

        mockCurrentUser.Setup(u => u.Id).Returns(userId);

        var controller = new InvitationsController(mockService.Object, mockCurrentUser.Object);
        var result     = await controller.UpdateInvitationStatus(1, new UpdateInvitationStatusRequest { Status = InvitationStatus.Cancelled });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // Validator — empty freelancerId
    [Fact]
    public void CreateInvitationValidator_ShouldHaveError_WhenFreelancerIdEmpty()
    {
        var model = CreateValidInvitationRequest();
        model.FreelancerId = Guid.Empty;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.FreelancerId);
    }

    // Validator — message too long
    [Fact]
    public void CreateInvitationValidator_ShouldHaveError_WhenMessageTooLong()
    {
        var model = CreateValidInvitationRequest();
        model.Message = new string('x', 2001);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Message);
    }
}
