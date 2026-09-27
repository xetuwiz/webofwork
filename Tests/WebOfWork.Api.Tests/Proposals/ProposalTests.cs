using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using WebOfWork.Api.DTOs.Proposals;
using WebOfWork.Api.Validators.Proposals;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebOfWork.Api.Controllers;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Tests.Proposals;

public class ProposalTests
{
    private readonly CreateProposalValidator _validator = new();

    private CreateProposalRequest CreateValidProposalRequest() => new()
    {
        ProjectId    = 1,
        PackageId    = Guid.NewGuid(),
        CoverLetter  = "This is a well-written cover letter of sufficient length.",
        BidAmount    = 50000,
        DeliveryDays = 7
    };

    // 8.6 — Duplicate proposal → 400
    [Fact]
    public async Task CreateProposal_ShouldReturn400_WhenProposalAlreadyExists()
    {
        var mockService     = new Mock<IProposalService>();
        var mockCurrentUser = new Mock<ICurrentUser>();
        var mockValidator   = new Mock<IValidator<CreateProposalRequest>>();
        var userId          = Guid.NewGuid();

        mockCurrentUser.Setup(u => u.Id).Returns(userId);
        mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateProposalRequest>(), default))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());
        mockService
            .Setup(s => s.CreateProposalAsync(userId, It.IsAny<CreateProposalRequest>()))
            .ThrowsAsync(new InvalidOperationException("You have already submitted a proposal for this project."));

        var controller = new ProposalsController(mockService.Object, mockCurrentUser.Object);
        var result     = await controller.CreateProposal(CreateValidProposalRequest(), mockValidator.Object);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // 8.7 — Client submitting proposal → 403
    [Fact]
    public async Task CreateProposal_ShouldReturn403_WhenCallerIsClient()
    {
        var mockService     = new Mock<IProposalService>();
        var mockCurrentUser = new Mock<ICurrentUser>();
        var mockValidator   = new Mock<IValidator<CreateProposalRequest>>();
        var userId          = Guid.NewGuid();

        mockCurrentUser.Setup(u => u.Id).Returns(userId);
        mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateProposalRequest>(), default))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());
        mockService
            .Setup(s => s.CreateProposalAsync(userId, It.IsAny<CreateProposalRequest>()))
            .ThrowsAsync(new UnauthorizedAccessException("Clients cannot submit proposals."));

        var controller = new ProposalsController(mockService.Object, mockCurrentUser.Object);
        var result     = await controller.CreateProposal(CreateValidProposalRequest(), mockValidator.Object);

        result.Should().BeOfType<ForbidResult>();
    }

    // 8.8 — Package not owned by freelancer → 400
    [Fact]
    public async Task CreateProposal_ShouldReturn400_WhenPackageNotOwned()
    {
        var mockService     = new Mock<IProposalService>();
        var mockCurrentUser = new Mock<ICurrentUser>();
        var mockValidator   = new Mock<IValidator<CreateProposalRequest>>();
        var userId          = Guid.NewGuid();

        mockCurrentUser.Setup(u => u.Id).Returns(userId);
        mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateProposalRequest>(), default))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());
        mockService
            .Setup(s => s.CreateProposalAsync(userId, It.IsAny<CreateProposalRequest>()))
            .ThrowsAsync(new ArgumentException("Invalid package or you do not own it."));

        var controller = new ProposalsController(mockService.Object, mockCurrentUser.Object);
        var result     = await controller.CreateProposal(CreateValidProposalRequest(), mockValidator.Object);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // 8.12b — Freelancer can only withdraw pending proposals
    [Fact]
    public async Task WithdrawProposal_ShouldReturn400_WhenProposalNotPending()
    {
        var mockService     = new Mock<IProposalService>();
        var mockCurrentUser = new Mock<ICurrentUser>();
        var userId          = Guid.NewGuid();

        mockCurrentUser.Setup(u => u.Id).Returns(userId);
        mockService
            .Setup(s => s.WithdrawProposalAsync(userId, 1))
            .ThrowsAsync(new InvalidOperationException("Only pending proposals can be withdrawn."));

        var controller = new ProposalsController(mockService.Object, mockCurrentUser.Object);
        var result     = await controller.WithdrawProposal(1);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // Validator — cover letter too short
    [Fact]
    public void CreateProposalValidator_ShouldHaveError_WhenCoverLetterTooShort()
    {
        var model = CreateValidProposalRequest();
        model.CoverLetter = "short";
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.CoverLetter);
    }

    // ──────────────────────────────────────────────────────────────────
    // BidAmount / DeliveryDays are deliberately NOT validated
    // ──────────────────────────────────────────────────────────────────
    //
    // These two used to assert a validation error for `BidAmount = 0` and
    // `DeliveryDays = 0`. Both rules were removed on purpose:
    // `CreateProposalAsync` overwrites both fields from the selected package's
    // own `Price` and `DeliveryDays` and never reads them, so requiring them
    // meant every client had to send two values that were silently discarded --
    // and it rejected the web client, which sends only
    // `{ project, package, cover_letter }`.
    //
    // Replaced with a single test that asserts the actual contract, so a future
    // edit that re-adds the rules breaks something that describes the intent
    // rather than something that merely records a number.
    [Fact]
    public void CreateProposalValidator_ShouldNotValidate_BidAmountOrDeliveryDays_SinceServiceOverwritesThem()
    {
        var model = CreateValidProposalRequest();
        model.BidAmount = 0;
        model.DeliveryDays = 0;

        var result = _validator.TestValidate(model);

        // Valid overall. This is the assertion that carries the intent: the web
        // client sends `{ project, package, cover_letter }` and nothing else, and
        // that payload must be accepted. Asserting "no error on these two fields"
        // alone would still pass if some unrelated rule started rejecting the
        // request.
        result.IsValid.Should().BeTrue(
            "a proposal with a project, package and cover letter is complete; the price " +
            "and delivery time come from the package");

        result.ShouldNotHaveValidationErrorFor(x => x.BidAmount);
        result.ShouldNotHaveValidationErrorFor(x => x.DeliveryDays);
    }
}
