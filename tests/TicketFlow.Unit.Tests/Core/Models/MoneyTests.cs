using System.Text.Json;
using System.Text.Json.Serialization;
using Shouldly;
using TicketFlow.Core.Models;
using Xunit;

namespace TicketFlow.Unit.Tests.Core.Models;

public sealed class MoneyTests
{
    [Fact]
    public void Constructor_WithNegativeAmount_ShouldThrow()
    {
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => new Money(-1m, "USD"));

        ex.ParamName.ShouldBe("amount");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyOrWhitespaceCurrency_ShouldThrow(string currency)
    {
        var ex = Should.Throw<ArgumentException>(() => new Money(10m, currency));

        ex.ParamName.ShouldBe("currency");
    }

    [Fact]
    public void Constructor_WithNullCurrency_ShouldThrow()
    {
        var ex = Should.Throw<ArgumentException>(() => new Money(10m, null!));

        ex.ParamName.ShouldBe("currency");
    }

    [Fact]
    public void AmountAndCurrency_Setters_ShouldNotBePublic()
    {
        var amountSetter = typeof(Money).GetProperty(nameof(Money.Amount))!.SetMethod;
        var currencySetter = typeof(Money).GetProperty(nameof(Money.Currency))!.SetMethod;

        amountSetter.ShouldNotBeNull();
        currencySetter.ShouldNotBeNull();
        amountSetter.IsPublic.ShouldBeFalse();
        currencySetter.IsPublic.ShouldBeFalse();
    }

    [Fact]
    public void Deserialize_WithNegativeAmount_ShouldThrow()
    {
        const string json = """
                            {
                                "Amount": -1,
                                "Currency": "USD"
                            }
                            """;

        var ex = Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<Money>(json));

        ex.ToString().ShouldContain("Amount cannot be negative");
    }

    [Fact]
    public void Constructor_ShouldBeMarkedWithJsonConstructor()
    {
        var ctor = typeof(Money).GetConstructor(new[] { typeof(decimal), typeof(string) });

        ctor.ShouldNotBeNull();
        ctor!.GetCustomAttributes(typeof(JsonConstructorAttribute), false).Length.ShouldBeGreaterThan(0);
    }
}
