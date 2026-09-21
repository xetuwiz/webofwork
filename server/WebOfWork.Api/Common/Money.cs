namespace WebOfWork.Api.Common;

/// <summary>
/// Money Value Object enforcing decimal(18,2) and "USD" currency.
/// Negative amounts are rejected. Only USD is permitted.
/// Used globally for all financial transactions.
/// </summary>
public record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = "USD")
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));
        
        if (!string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Currency must be USD. No other currencies are supported.", nameof(currency));

        Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency.ToUpperInvariant();
    }

    public static Money operator +(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.Amount - b.Amount, a.Currency);
    }

    public static Money operator *(Money a, decimal multiplier)
    {
        if (multiplier < 0)
            throw new ArgumentException("Multiplier cannot be negative.", nameof(multiplier));
        return new Money(a.Amount * multiplier, a.Currency);
    }

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException($"Cannot perform arithmetic on different currencies: {a.Currency} and {b.Currency}");
    }

    public override string ToString() => $"{Currency} {Amount:F2}";
}
