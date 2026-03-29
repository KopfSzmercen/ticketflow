namespace TicketFlow.Functions.Waitlist;

public enum WaitlistOfferDecision
{
    Accept,
    Reject
}

public static class WaitlistOfferDecisionContract
{
    public const string EventName = "WaitlistDecision";
    public const string AcceptValue = "accept";
    public const string RejectValue = "reject";

    public static bool TryParse(string? decision, out WaitlistOfferDecision parsedDecision)
    {
        if (string.Equals(decision, AcceptValue, StringComparison.OrdinalIgnoreCase))
        {
            parsedDecision = WaitlistOfferDecision.Accept;
            return true;
        }

        if (string.Equals(decision, RejectValue, StringComparison.OrdinalIgnoreCase))
        {
            parsedDecision = WaitlistOfferDecision.Reject;
            return true;
        }

        parsedDecision = default;
        return false;
    }

    public static string ToEventPayload(this WaitlistOfferDecision decision)
        => decision switch
        {
            WaitlistOfferDecision.Accept => AcceptValue,
            WaitlistOfferDecision.Reject => RejectValue,
            _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unsupported waitlist offer decision.")
        };
}