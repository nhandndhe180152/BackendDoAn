using System;

namespace Backend.Share.Helpers;

public static class PasswordHelper
{
    /// <summary>
    /// Hash mật khẩu bằng BCrypt trước khi lưu DB. Không lưu mật khẩu dạng plain text.
    /// </summary>
    /// <param>Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    /// <summary>
    /// Xác thực mật khẩu người dùng bằng cách so sánh input với BCrypt hash đã lưu.
    /// </summary>
    /// <param name="input">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <param name="hashed">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public static bool VerifyPassword(string input, string hashed)
    {
        return BCrypt.Net.BCrypt.Verify(input, hashed);
    }
}
