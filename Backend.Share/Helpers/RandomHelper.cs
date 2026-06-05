using System;
using System.Security.Cryptography;
using System.Text;

namespace Backend.Share.Helpers;

/// <summary>
/// Helper sinh chuỗi random và OTP bằng RandomNumberGenerator an toàn hơn Random thường.
/// </summary>
public static class RandomHelper
{
    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

    /// <summary>
    /// Sinh chuỗi ngẫu nhiên an toàn bằng RandomNumberGenerator, dùng cho token/key/code không cần chỉ gồm số.
    /// </summary>
    /// <param name="length">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public static string GenerateRandomString(int length = 10)
    {
        var bytes = new byte[length];
        Rng.GetBytes(bytes);
        var result = new StringBuilder(length);

        foreach (var b in bytes)
        {
            result.Append(Chars[b % Chars.Length]);
        }

        return result.ToString();
    }

    /// <summary>
    /// Sinh mã OTP chỉ gồm chữ số bằng RandomNumberGenerator để dùng cho xác thực email/phone hoặc reset password.
    /// </summary>
    /// <param name="length">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public static string GenerateOtpCode(int length = 6)
    {
        var digits = "0123456789";
        var bytes = new byte[length];
        Rng.GetBytes(bytes);
        var otp = new StringBuilder(length);

        foreach (var b in bytes)
        {
            otp.Append(digits[b % digits.Length]);
        }

        return otp.ToString();
    }
}
