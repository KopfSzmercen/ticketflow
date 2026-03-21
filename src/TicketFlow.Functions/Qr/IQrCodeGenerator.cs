namespace TicketFlow.Functions.Qr;

public interface IQrCodeGenerator
{
    byte[] GeneratePng(string payload);
}