using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace WebOfWork.Api.Tests.Common;

/// <summary>
/// Guards against two actions claiming the same route and HTTP verb.
/// </summary>
/// <remarks>
/// <para>
/// ASP.NET Core resolves a request by matching (route template, HTTP method).
/// When two actions match, it does not pick one -- it throws
/// <c>AmbiguousMatchException</c> and the caller gets a 500.
/// </para>
/// <para>
/// That failure has three properties that make it exceptionally good at
/// surviving:
///
/// <list type="bullet">
/// <item>It is not a compile error. Two methods with different parameter lists are
/// a perfectly legal C# overload, so the build stays green.</item>
/// <item>It is not a unit-test failure. Every existing test calls a service
/// directly, and nothing constructs the MVC action table.</item>
/// <item>It is not visible until a request arrives -- and for an admin-only
/// endpoint, that can be long after the code shipped.</item>
/// </list>
///
/// <para>
/// This bug happened for real here. <c>AiWorkflowController</c> had two
/// <c>ListWorkflows</c> methods, both <c>[HttpGet]</c> on the bare route: the
/// original four-parameter one and a richer six-parameter one added later. The
/// compiler was satisfied, all 267 tests passed, and the admin workflow monitor
/// would have thrown on its first call.
/// </para>
/// <para>
/// The fix is to assert the invariant over every controller in the assembly, so
/// the next one is caught at build time rather than at request time.
/// </para>
/// </remarks>
public class RouteUniquenessTests
{
    /// <summary>Controllers are the only place actions live.</summary>
    private static IEnumerable<Type> Controllers()
    {
        return typeof(Controllers.ProjectsController).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));
    }

    /// <summary>
    /// The verbs this project uses, keyed by the attribute that declares them.
    /// </summary>
    /// <remarks>
    /// Read from the attribute *type name* rather than through
    /// <c>IHttpMethodMetadata</c>, whose shape changed between ASP.NET Core
    /// versions (it went from an <c>HttpMethods</c> property to extending
    /// <c>IReadOnlyList&lt;string&gt;</c>). A name lookup cannot break on an
    /// SDK upgrade, and it fails loudly by finding nothing if a verb attribute
    /// is renamed -- which the sanity test below would catch.
    /// </remarks>
    private static readonly (string Attribute, string Verb)[] VerbAttributes =
    {
        ("HttpGetAttribute", "GET"),
        ("HttpPostAttribute", "POST"),
        ("HttpPutAttribute", "PUT"),
        ("HttpPatchAttribute", "PATCH"),
        ("HttpDeleteAttribute", "DELETE"),
        ("HttpHeadAttribute", "HEAD"),
        ("HttpOptionsAttribute", "OPTIONS"),
    };

    /// <summary>
    /// Attributes that carry a route template but no verb, so the action answers
    /// all of them.
    /// </summary>
    private static readonly HashSet<string> VerblessTemplateAttributes =
        new(StringComparer.Ordinal) { "RouteAttribute", "AcceptVerbsAttribute" };

    private static readonly string[] AllVerbs =
        { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" };

    /// <summary>
    /// Every (verb, route) pair an action answers, as the router would see it.
    /// </summary>
    private static IEnumerable<(string Verb, string Route, string Action, string Controller)>
        Routes()
    {
        foreach (var controller in Controllers())
        {
            // The controller-level [Route], e.g. "api/ai/workflows". A controller
            // with none uses its own name, which is what the convention-based
            // router does.
            var prefix = controller
                .GetCustomAttribute<RouteAttribute>()?.Template
                ?? controller.Name.Replace("Controller", "");

            foreach (var method in controller
                         .GetMethods(BindingFlags.Public | BindingFlags.Instance
                                     | BindingFlags.DeclaredOnly))
            {
                if (method.DeclaringType != controller) continue;
                if (method.GetCustomAttributes<NonActionAttribute>().Any()) continue;

                foreach (var (verb, route) in RoutesFor(method))
                {
                    // Expanded *after* joining, because the token is usually in
                    // the controller-level prefix: `[Route("api/[controller]")]`
                    // is one shared-looking attribute across four controllers
                    // that in fact produce four different routes. Comparing the
                    // raw text would report all four as colliding.
                    var joined = Join(prefix, route);

                    yield return (verb, Normalise(Expand(joined, controller, method)),
                        method.Name, controller.Name);
                }
            }
        }
    }

    /// <summary>
    /// The (verb, route) pairs a single action method declares.
    /// </summary>
    private static IEnumerable<(string Verb, string Route)> RoutesFor(MethodInfo method)
    {
        foreach (var attribute in method.GetCustomAttributes())
        {
            var name = attribute.GetType().Name;

            var verb = VerbAttributes
                .FirstOrDefault(v => v.Attribute == name).Verb;

            var verbless = VerblessTemplateAttributes.Contains(name);
            if (verb is null && !verbless) continue;

            // The template is read reflectively rather than through
            // `IHttpRouteTemplateProvider`, which is not part of the public API.
            // Every attribute carrying one -- HttpGetAttribute, RouteAttribute,
            // AcceptVerbsAttribute -- exposes a public `Template` string.
            var template = attribute.GetType()
                .GetProperty("Template", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(attribute) as string;

            // A verbless attribute on an action is answered under every verb, so
            // a same-route typed action really would collide with it.
            var verbs = verb is null ? AllVerbs : new[] { verb };

            // "[HttpGet(\"a\", \"b\")]" declares two routes, and only one of them
            // may collide.
            var alternatives = string.IsNullOrEmpty(template)
                ? new[] { string.Empty }
                : template.Split(',').Select(t => t.Trim()).ToArray();

            foreach (var v in verbs)
            {
                foreach (var alternative in alternatives)
                {
                    yield return (v, alternative);
                }
            }
        }
    }

    private static string Join(string prefix, string route)
    {
        if (string.IsNullOrEmpty(route)) return prefix;
        // A leading slash on the method template replaces the whole prefix, which
        // is how ASP.NET's route combination works.
        if (route.StartsWith('/') || route.StartsWith('~')) return route.TrimStart('~');
        return $"{prefix.TrimEnd('/')}/{route.TrimStart('/')}";
    }

    /// <summary>
    /// Expands the route tokens the default template supports.
    /// </summary>
    /// <remarks>
    /// This project uses <c>[Route("api/[controller]")]</c> on several
    /// controllers, which looks like one shared route and is not: each expands to
    /// its own. Only the two tokens actually in use are handled. An unknown token
    /// is left in place rather than blanked, so a controller that starts using
    /// <c>[area]</c> produces a visibly odd route in the failure message instead
    /// of a silently wrong one.
    /// </remarks>
    private static string Expand(string route, Type controller, MethodInfo method)
    {
        if (string.IsNullOrEmpty(route)) return route;

        // The router strips the "Controller" and "Async" suffixes, so
        // `BriefsController.GetBriefAsync` is `briefs` and `getbrief`.
        var controllerToken = controller.Name.Replace("Controller", "");
        var actionToken = method.Name.Replace("Async", "");

        return route
            .Replace("[controller]", controllerToken, StringComparison.OrdinalIgnoreCase)
            .Replace("[action]", actionToken, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Canonical form for comparison: no leading or trailing slash, no repeated
    /// slash, no case difference.
    /// </summary>
    /// <remarks>
    /// Case is folded because route matching is case-insensitive, so
    /// "api/Projects" and "api/projects" really are the same route. Without that
    /// fold this test would report two endpoints that the router cannot tell
    /// apart.
    /// </remarks>
    private static string Normalise(string route)
    {
        var collapsed = Regex.Replace(route.Trim().Trim('/'), "/+", "/");
        return collapsed.ToLowerInvariant();
    }

    [Fact]
    public void NoTwoActions_ClaimTheSameRouteAndVerb()
    {
        var clashes = Routes()
            .GroupBy(r => (r.Verb, r.Route))
            .Where(g => g.Select(x => $"{x.Controller}.{x.Action}").Distinct().Count() > 1)
            .Select(g => $"  {g.Key.Verb} {g.Key.Route}\n    "
                + string.Join("\n    ", g.Select(x => $"{x.Controller}.{x.Action}")))
            .ToList();

        clashes.Should().BeEmpty(
            "ASP.NET throws AmbiguousMatchException on the first request to a route " +
            "that two actions answer, and neither the compiler nor a unit test " +
            "catches it. Collisions:\n" + string.Join("\n", clashes));
    }

    /// <summary>
    /// The specific collision that occurred, pinned so a reintroduction names
    /// itself rather than appearing as one line in a list.
    /// </summary>
    [Fact]
    public void AiWorkflowController_HasExactlyOneListAction()
    {
        var lists = Routes()
            .Where(r => r.Controller == "AiWorkflowController"
                        && r.Route == "api/ai/workflows"
                        && r.Verb == "GET")
            .ToList();

        lists.Should().ContainSingle(
            "there were two [HttpGet] ListWorkflows actions on this route. They were " +
            "legal C# overloads, so the build stayed green and the admin monitor " +
            "would have thrown AmbiguousMatchException on its first call");
    }

    /// <summary>
    /// A sanity floor. Without it the test above would pass vacuously if the
    /// reflection broke and started finding nothing -- which is how a guard test
    /// quietly stops guarding.
    /// </summary>
    [Fact]
    public void TheRouteScan_ActuallyFindsActions()
    {
        var all = Routes().ToList();

        all.Should().HaveCountGreaterThan(100,
            "the reflection found almost no actions, so the uniqueness check above " +
            "is passing vacuously");
        all.Select(r => r.Controller).Distinct().Should().HaveCountGreaterThan(20,
            "fewer than 20 controllers were scanned");
    }
}
