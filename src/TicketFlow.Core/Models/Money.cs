namespace TicketFlow.Core.Models;

public enum Currency
{
    Usd,
    Eur,
    Gbp,
    Jpy
}

public sealed record Money
{
    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; init; }
    public string Currency { get; init; }
}
