namespace TicketFlow.Functions.Waitlist;

public sealed class WaitlistOptions
{
    public const string SectionName = "Waitlist";

    public int OfferDurationInMinutes { get; set; } = 15;
}