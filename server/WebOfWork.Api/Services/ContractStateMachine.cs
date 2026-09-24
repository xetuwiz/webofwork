using System;
using System.Collections.Generic;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public static class ContractStateMachine
{
    private static readonly Dictionary<ContractStatus, HashSet<ContractStatus>> ValidTransitions = new()
    {
        { ContractStatus.Offered, new HashSet<ContractStatus> { ContractStatus.Active, ContractStatus.Cancelled } },
        { ContractStatus.Active, new HashSet<ContractStatus> { ContractStatus.Submitted, ContractStatus.Cancelled, ContractStatus.Disputed } },
        { ContractStatus.Submitted, new HashSet<ContractStatus> { ContractStatus.Completed, ContractStatus.Active, ContractStatus.Disputed } },
        { ContractStatus.Disputed, new HashSet<ContractStatus> { ContractStatus.Completed, ContractStatus.Refunded } },
        { ContractStatus.Completed, new HashSet<ContractStatus>() },
        { ContractStatus.Cancelled, new HashSet<ContractStatus>() },
        { ContractStatus.Refunded, new HashSet<ContractStatus>() }
    };

    public static bool IsValidTransition(ContractStatus currentStatus, ContractStatus newStatus)
    {
        if (currentStatus == newStatus) return true;

        if (ValidTransitions.TryGetValue(currentStatus, out var allowedStates))
        {
            return allowedStates.Contains(newStatus);
        }

        return false;
    }
}
