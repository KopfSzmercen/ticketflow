namespace TicketFlow.Core.Models;

public enum OrderStatus
{
    /// <summary>Order created; workflow has not yet started processing.</summary>
    Pending,

    /// <summary>Workflow is in the process of reserving a ticket.</summary>
    Reserving,

    /// <summary>Ticket reserved; workflow is processing payment.</summary>
    Paying,

    /// <summary>Payment succeeded; order is complete.</summary>
    Confirmed,

    /// <summary>Order failed — reservation failed or payment declined. Any reserved inventory has been released.</summary>
    Failed
}
