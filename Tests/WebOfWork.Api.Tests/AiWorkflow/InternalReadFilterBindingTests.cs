using System.Net;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using WebOfWork.Api.Controllers;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.AiWorkflow;

/// <summary>
/// Do the internal read filters actually filter?
/// </summary>
/// <remarks>
/// <para>
/// The agent client sends query parameters in snake_case, following this project's
/// own wire convention. Query-string binding on the .NET side is case-insensitive
/// but <em>not</em> underscore-insensitive, so <c>?category_id=3&amp;min_rating=4.5</c>
/// bound to nothing and both filters were silently dropped.
/// </para>
/// <para>
/// The consequence was not an error. <c>GET /internal/read/freelancers</c>
/// returned <c>200</c> with every freelancer on the platform, so the matching
/// agent ranked the entire pool against a request that had asked for one category
/// above a rating. <c>query</c>, <c>available</c> and <c>limit</c> are single
/// words and bound correctly, which is what made the call look entirely plausible.
/// </para>
/// <para>
/// Asserted through the controller signature by reading the attributes, because
/// the alternative -- invoking the endpoint and checking the rows -- depends on
/// which spelling the test happens to send and would pass for the wrong reason if
/// only one of the two were bound.
/// </para>
/// </remarks>
public sealed class InternalReadFilterBindingTests
{
    [Fact]
    public void Freelancer_search_accepts_the_category_filter_in_either_spelling()
    {
        AcceptsBoth(
            typeof(InternalReadController).GetMethod(nameof(InternalReadController.SearchFreelancers))!,
            "category_id",
            "categoryId");
    }

    [Fact]
    public void Freelancer_search_accepts_the_rating_filter_in_either_spelling()
    {
        AcceptsBoth(
            typeof(InternalReadController).GetMethod(nameof(InternalReadController.SearchFreelancers))!,
            "min_rating",
            "minRating");
    }

    [Fact]
    public void Similar_projects_accepts_the_exclusion_in_either_spelling()
    {
        // Snake_case was the only spelling accepted here, so a camelCase caller
        // silently received the very project it was comparing against.
        AcceptsBoth(
            typeof(InternalReadController).GetMethod(nameof(InternalReadController.SimilarProjects))!,
            "exclude_project_id",
            "excludeProjectId");
    }

    [Fact]
    public void Price_bands_still_accepts_both_spellings()
    {
        // Guard against a future tidy-up collapsing these to one spelling.
        AcceptsBoth(
            typeof(InternalReadController).GetMethod(nameof(InternalReadController.GetPriceBands))!,
            "category_id",
            "categoryId");
    }

    /// <summary>
    /// Asserts the action binds both spellings, and that they resolve to the same
    /// value rather than one shadowing the other.
    /// </summary>
    private static void AcceptsBoth(MethodInfo method, string snake, string camel)
    {
        var bound = method.GetParameters()
            .Select(p => p.GetCustomAttribute<FromQueryAttribute>()?.Name ?? p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bound.Should().Contain(snake, $"{method.Name} does not bind the snake_case spelling");
        bound.Should().Contain(camel, $"{method.Name} does not bind the camelCase spelling");

        // Both spellings must feed one parameter. Two parameters, one preferred
        // with `??`, is how `GetPriceBands` does it; anything else means the two
        // spellings resolve to different values and one filter is dead.
        var distinct = method.GetParameters()
            .Where(p => (p.GetCustomAttribute<FromQueryAttribute>()?.Name ?? p.Name)
                        is var n && (n.Equals(snake, StringComparison.OrdinalIgnoreCase)
                                      || n.Equals(camel, StringComparison.OrdinalIgnoreCase)))
            .Select(p => p.Name)
            .ToList();

        distinct.Should().HaveCount(
            2,
            $"{method.Name} declares {distinct.Count} parameter(s) for {snake}/{camel}; "
            + "both spellings must be distinct parameters so one can fall back to the other");
    }
}