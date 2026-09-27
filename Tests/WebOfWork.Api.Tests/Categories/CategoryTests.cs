using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using FluentAssertions;
using Moq;
using WebOfWork.Api.Controllers;
using WebOfWork.Api.DTOs.Categories;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Tests.Categories;

public class CategoryTests
{
    // Auth checks (like [Authorize(Policy="RequireAdmin")]) are applied by
    // middleware, so they are covered by the E2E suites that spin up
    // Testcontainers and act as real users.
    //
    // ## These two tests assert a throw, not a 400
    //
    // They previously asserted `BadRequestObjectResult`, on the assumption that
    // the controller caught the exception itself. It does not -- and it should
    // not: only 5 of the 30 controllers in this project wrap actions in
    // try/catch. The house convention is to let a business-rule violation
    // propagate and let `ExceptionHandlingMiddleware` translate it, which maps
    // `InvalidOperationException` to 400 Bad Request.
    //
    // So a caller still gets a 400 over HTTP; the difference is that the mapping
    // is asserted where it happens. Testing it here would have required either
    // duplicating the controller's catch, or standing up the middleware -- and
    // the first option is how two disagreeing translation layers get created.
    // `ExceptionHandlingMiddleware`'s own tests cover the mapping.

    [Fact]
    public async Task DeleteCategoryAsync_ShouldThrow_WhenProfilesExist()
    {
        var mockService = new Mock<ICategoryService>();
        mockService.Setup(s => s.DeleteCategoryAsync(It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Cannot delete a category that has associated skills or profiles."));

        var controller = new CategoriesController(mockService.Object);

        // The action is `Delete`, not `DeleteCategory`.
        var act = async () => await controller.Delete(1);

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("Cannot delete a category that has associated skills or profiles.");
    }

    [Fact]
    public async Task DeleteSkillAsync_ShouldThrow_WhenInUse()
    {
        var mockService = new Mock<ICategoryService>();
        mockService.Setup(s => s.DeleteSkillAsync(It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Cannot delete a skill that is used by freelancers."));

        var controller = new CategoriesController(mockService.Object);

        var act = async () => await controller.DeleteSkill(1);

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("Cannot delete a skill that is used by freelancers.");
    }

    /// <summary>
    /// The skills filter is bound from both spellings of the category parameter.
    /// </summary>
    /// <remarks>
    /// The serialiser is snake_case, which is the wire convention for this
    /// project -- but <c>PropertyNamingPolicy</c> applies to JSON bodies and
    /// responses only. It does not rewrite query-string binding, so a bare
    /// <c>[FromQuery] int? categoryId</c> is reachable only as <c>?categoryId=</c>.
    /// A caller sending <c>?category_id=1</c>, following the project's own
    /// convention, got no error and no filter: all 654 skills instead of the
    /// subset for one category. Silent, and wrong in the direction that looks
    /// plausible.
    ///
    /// Asserted through the controller rather than the service, because the
    /// binding is the thing that was broken -- the service filtered correctly
    /// every time it was given a category, which is why this never showed up in
    /// a service-level test.
    /// </remarks>
    [Theory]
    [InlineData("snake")]
    [InlineData("camel")]
    public async Task Skills_FiltersByCategory_WhicheverSpellingTheCallerUses(string spelling)
    {
        var mockService = new Mock<ICategoryService>();
        mockService.Setup(s => s.GetSkillsAsync(7))
            .ReturnsAsync(new List<SkillResponse> { new() { Id = 1, Name = "React", CategoryId = 7 } });

        var controller = new CategoriesController(mockService.Object);

        var result = await controller.Skills(
            categoryIdSnake: spelling == "snake" ? 7 : null,
            categoryIdCamel: spelling == "camel" ? 7 : null,
            search: null, no_pagination: true, page: 1, page_size: 20);

        mockService.Verify(s => s.GetSkillsAsync(7), Times.Once,
            "the filter must reach the service rather than being silently dropped");
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Skills_WithNoCategoryFilter_AsksTheServiceForEverything()
    {
        var mockService = new Mock<ICategoryService>();
        mockService.Setup(s => s.GetSkillsAsync(null))
            .ReturnsAsync(new List<SkillResponse> { new() { Id = 1, Name = "React" } });

        var controller = new CategoriesController(mockService.Object);

        await controller.Skills(
            categoryIdSnake: null, categoryIdCamel: null,
            search: null, no_pagination: true, page: 1, page_size: 20);

        mockService.Verify(s => s.GetSkillsAsync(null), Times.Once);
    }
}
