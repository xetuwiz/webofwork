using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Projects;
using WebOfWork.Api.Models;
using WebOfWork.Api.Validators.Projects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebOfWork.Api.Controllers;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Tests.Projects;

public class ProjectTests
{
    private readonly CreateProjectValidator _validator = new();

    private CreateProjectRequest CreateValidRequest()
    {
        // `PricingType` is a `string` on the DTO, not the `ProjectPricingType`
        // enum the entity uses. The 1/2 form is only accepted by
        // `FlexiblePricingTypeConverter`, which runs at the JSON boundary -- a
        // test that builds the object in C# has to use the name.
        return new CreateProjectRequest
        {
            Title = "Valid title length",
            Description = "Valid description length that is long enough.",
            PricingType = "fixed",
            FixedPrice = 100,
            ExpectedDays = 5
        };
    }

    [Fact]
    public void CreateProjectValidator_ShouldHaveError_WhenFixedPricingMissingFixedPrice()
    {
        var model = CreateValidRequest();
        model.PricingType = "fixed";
        model.FixedPrice = null;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.FixedPrice);
    }

    [Fact]
    public void CreateProjectValidator_ShouldHaveError_WhenRangePricingMissingMinOrMaxPrice()
    {
        var model = CreateValidRequest();
        model.PricingType = "range";
        model.FixedPrice = null;
        model.MinPrice = null;
        model.MaxPrice = 100;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.MinPrice);
        
        var model2 = CreateValidRequest();
        model2.PricingType = "range";
        model2.FixedPrice = null;
        model2.MinPrice = 100;
        model2.MaxPrice = null;
        var result2 = _validator.TestValidate(model2);
        result2.ShouldHaveValidationErrorFor(x => x.MaxPrice);
    }

    [Fact]
    public async Task Update_ShouldReturnForbidden_WhenNotOwner()
    {
        var mockService = new Mock<IProjectService>();
        var mockRecommendation = new Mock<IProjectRecommendationService>();
        var mockCurrentUser = new Mock<ICurrentUser>();

        var clientId = Guid.NewGuid();
        mockCurrentUser.Setup(s => s.Id).Returns(clientId);

        var validRequest = new UpdateProjectRequest
        {
            Title = "Valid title here",
            Description = "This is a long enough description that satisfies the minimum length rule.",
            ExpectedDays = 10,
            SkillIds = new List<int>()
        };

        mockService.Setup(s => s.UpdateProjectAsync(clientId, 1, It.IsAny<UpdateProjectRequest>()))
            .ThrowsAsync(new UnauthorizedAccessException("You do not own this project."));

        // The controller takes a DbContext now, for the browse query. Nothing in
        // this path touches it, so an in-memory instance is enough and avoids
        // standing up a container for a test that never queries.
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new WebOfWorkDbContext(options);

        var controller = new ProjectsController(
            mockService.Object, mockRecommendation.Object, mockCurrentUser.Object, db);

        var result = await controller.Update(1, validRequest);

        // 403, not 400. The action used to catch every exception as
        // `BadRequest(new { error })`, so "you do not own this project" arrived as
        // 400 -- which tells a client to fix its input, and retrying a corrected
        // payload fails identically forever. `ToErrorResult` now maps
        // `UnauthorizedAccessException` to 403 so the UI can tell "malformed" from
        // "not yours".
        // `StatusCode(403, body)` returns an `ObjectResult` carrying the status,
        // not a bare `StatusCodeResult` -- so the body is reachable.
        var forbidden = result.Should().BeOfType<ObjectResult>().Subject;
        forbidden.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        // The body still carries the reason, so the user is not left guessing.
        var body = forbidden.Value;
        body.Should().NotBeNull();
        body!.ToString().Should().Contain("You do not own this project.");
    }
}
