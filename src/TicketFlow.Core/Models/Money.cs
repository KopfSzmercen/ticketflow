using System.Text.Json.Serialization;

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
    [JsonConstructor]
    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required.", nameof(currency));

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; private init; }
    public string Currency { get; private init; }
}
