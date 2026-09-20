using FluentAssertions;
using WebOfWork.Api.Common;
using Xunit;

namespace WebOfWork.Api.Tests.Common;

public class MoneyTests
{
    [Fact]
    public void Constructor_WithValidAmountAndLKR_CreatesSuccessfully()
    {
        // Act
        var money = new Money(1000.55m, "LKR");

        // Assert
        money.Amount.Should().Be(1000.55m);
        money.Currency.Should().Be("LKR");
    }

    [Fact]
    public void Constructor_WithNegativeAmount_ThrowsArgumentException()
    {
        // Act
        Action act = () => new Money(-100m, "LKR");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public void Constructor_WithNonLKRCurrency_ThrowsArgumentException()
    {
        // Act
        Action act = () => new Money(100m, "USD");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must be LKR*");
    }

    [Theory]
    [InlineData(100.554, 100.55)]
    [InlineData(100.555, 100.56)]
    [InlineData(100.559, 100.56)]
    public void Constructor_RoundsToTwoDecimalPlaces(decimal input, decimal expected)
    {
        // Act
        var money = new Money(input, "LKR");

        // Assert
        money.Amount.Should().Be(expected);
    }

    [Fact]
    public void OperatorAdd_WithTwoMoneys_AddsAmounts()
    {
        // Arrange
        var m1 = new Money(100m);
        var m2 = new Money(50m);

        // Act
        var result = m1 + m2;

        // Assert
        result.Amount.Should().Be(150m);
        result.Currency.Should().Be("LKR");
    }

    [Fact]
    public void OperatorSubtract_WithTwoMoneys_SubtractsAmounts()
    {
        // Arrange
        var m1 = new Money(100m);
        var m2 = new Money(30m);

        // Act
        var result = m1 - m2;

        // Assert
        result.Amount.Should().Be(70m);
        result.Currency.Should().Be("LKR");
    }

    [Fact]
    public void OperatorSubtract_ResultingInNegativeAmount_ThrowsArgumentException()
    {
        // Arrange
        var m1 = new Money(30m);
        var m2 = new Money(100m);

        // Act
        Action act = () => { var res = m1 - m2; };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public void OperatorMultiply_WithDecimal_MultipliesAmount()
    {
        // Arrange
        var m1 = new Money(100m);

        // Act
        var result = m1 * 1.5m;

        // Assert
        result.Amount.Should().Be(150m);
        result.Currency.Should().Be("LKR");
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var money = new Money(1000.5m);

        // Act
        var result = money.ToString();

        // Assert
        result.Should().Be("LKR 1000.50");
    }
}
