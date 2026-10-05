using System;
using Xunit;
using FluentAssertions;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Tests.Contracts;

public class ContractStateTests
{
    [Theory]
    [InlineData(ContractStatus.Offered, ContractStatus.Active)]
    [InlineData(ContractStatus.Offered, ContractStatus.Cancelled)]
    [InlineData(ContractStatus.Active, ContractStatus.Submitted)]
    [InlineData(ContractStatus.Submitted, ContractStatus.Completed)]
    [InlineData(ContractStatus.Submitted, ContractStatus.Active)] // Revise
    public void IsValidTransition_ShouldReturnTrue_ForValidTransitions(ContractStatus from, ContractStatus to)
    {
        var result = ContractStateMachine.IsValidTransition(from, to);
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(ContractStatus.Offered, ContractStatus.Completed)]
    [InlineData(ContractStatus.Completed, ContractStatus.Active)]
    [InlineData(ContractStatus.Cancelled, ContractStatus.Active)]
    [InlineData(ContractStatus.Submitted, ContractStatus.Offered)]
    public void IsValidTransition_ShouldReturnFalse_ForInvalidTransitions(ContractStatus from, ContractStatus to)
    {
        var result = ContractStateMachine.IsValidTransition(from, to);
        result.Should().BeFalse();
    }
}
