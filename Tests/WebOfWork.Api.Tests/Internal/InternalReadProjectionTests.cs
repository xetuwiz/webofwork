using FluentAssertions;
using WebOfWork.Api.DTOs.Internal;
using Xunit;

namespace WebOfWork.Api.Tests.Internal;

public sealed class InternalReadProjectionTests
{
    [Fact]
    public void FreelancerProjection_DoesNotExposePrivateIdentityFields()
    {
        var properties = typeof(InternalFreelancerListDto).GetProperties()
            .Select(property => property.Name);

        properties.Should().NotContain("Email");
        properties.Should().NotContain("Phone");
        properties.Should().NotContain("PasswordHash");
        properties.Should().Contain(new[] { "FullName", "Skills", "Packages" });
    }

    [Fact]
    public void InternalMoneyProjections_DeclareUsdSemantics()
    {
        typeof(InternalPackageDto).GetProperty(nameof(InternalPackageDto.PriceUsd)).Should().NotBeNull();
        typeof(InternalCommissionDto).GetProperty(nameof(InternalCommissionDto.Currency)).Should().NotBeNull();
    }
}