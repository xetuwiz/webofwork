using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace WebOfWork.Api.Authorization;

public class ContractParticipantRequirement : IAuthorizationRequirement
{
}

public class ContractParticipantHandler : AuthorizationHandler<ContractParticipantRequirement, int>
{
    // Simplified requirement since we check participant status in services,
    // but this serves as a foundation for attribute-based resource auth if needed later.
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ContractParticipantRequirement requirement, int contractId)
    {
        // Actual DB check would go here if not handled at service layer.
        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
