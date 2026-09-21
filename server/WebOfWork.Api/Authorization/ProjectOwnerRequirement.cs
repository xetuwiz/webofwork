using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace WebOfWork.Api.Authorization;

public class ProjectOwnerRequirement : IAuthorizationRequirement
{
}

public class ProjectOwnerHandler : AuthorizationHandler<ProjectOwnerRequirement, int>
{
    // Simplified requirement since we check ownership in services,
    // but this serves as a foundation for attribute-based resource auth if needed later.
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ProjectOwnerRequirement requirement, int projectId)
    {
        // Actual DB check would go here if not handled at service layer.
        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
