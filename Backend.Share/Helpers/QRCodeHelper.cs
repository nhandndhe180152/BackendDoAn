using QRCoder;
using System;

namespace Backend.Share.Helpers;

public static class QRCodeHelper
{
    /// <summary>
    /// Generates a QR Code as a PNG byte array.
    /// </summary>
    /// <param name="text">The string content to encode.</param>
    /// <param name="pixelsPerModule">The size of each QR module (pixel resolution).</param>
    /// <returns>PNG image byte array.</returns>
    public static byte[] GenerateQRCodePng(string text, int pixelsPerModule = 10)
    {
        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("Text content for QR code cannot be null or empty.", nameof(text));
        }

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(pixelsPerModule);
    }
}
