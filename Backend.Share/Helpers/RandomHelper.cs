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

    /// <summary>
    /// Sinh mật khẩu tạm ngẫu nhiên, đảm bảo có đủ chữ hoa, chữ thường, chữ số và ký tự đặc biệt.
    /// Dùng khi admin tạo tài khoản hoặc reset mật khẩu qua "Quên mật khẩu" (bàn giao qua email,
    /// người dùng bắt buộc đổi ở lần đăng nhập kế tiếp).
    /// </summary>
    /// <param name="length">Độ dài mật khẩu (tối thiểu 8).</param>
    /// <returns>Mật khẩu tạm dạng chuỗi.</returns>
    public static string GeneratePassword(int length = 12)
    {
        if (length < 8) length = 8;

        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";   // bỏ I, O gây nhầm
        const string lower = "abcdefghijkmnpqrstuvwxyz";   // bỏ l, o gây nhầm
        const string digits = "23456789";                  // bỏ 0, 1 gây nhầm
        const string special = "!@#$%^&*";
        string all = upper + lower + digits + special;

        var chars = new char[length];
        // Bảo đảm mỗi nhóm có ít nhất 1 ký tự.
        chars[0] = upper[NextInt(upper.Length)];
        chars[1] = lower[NextInt(lower.Length)];
        chars[2] = digits[NextInt(digits.Length)];
        chars[3] = special[NextInt(special.Length)];
        for (int i = 4; i < length; i++)
            chars[i] = all[NextInt(all.Length)];

        // Xáo trộn để không cố định vị trí các nhóm.
        for (int i = length - 1; i > 0; i--)
        {
            int j = NextInt(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    private static int NextInt(int maxExclusive)
    {
        var bytes = new byte[4];
        Rng.GetBytes(bytes);
        return (int)(BitConverter.ToUInt32(bytes, 0) % (uint)maxExclusive);
    }
}
