using QRCoder;

namespace TicketFlow.Functions.Qr;

public sealed class QrCodeGenerator : IQrCodeGenerator
{
    public byte[] GeneratePng(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(qrData);
        return pngQrCode.GetGraphic(10);
    }
}