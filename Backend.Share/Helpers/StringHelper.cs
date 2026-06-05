using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Backend.Share.Helpers;

public static class StringHelper
{
    /// <summary>
    /// Tạo slug URL từ chuỗi đầu vào: chuyển lowercase, bỏ dấu tiếng Việt, xóa ký tự đặc biệt và thay khoảng trắng bằng dấu gạch ngang.
    /// </summary>
    /// <param name="input">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        string str = input.ToLowerInvariant();

        // Bước 1: Chuyển các ký tự tiếng Việt về dạng không dấu
        str = RemoveVietnameseDiacritics(str);

        // Bước 2: Xoá các ký tự không hợp lệ (chỉ giữ chữ cái, số, dấu cách và -)
        str = Regex.Replace(str, @"[^a-z0-9\s-]", "");

        // Bước 3: Đổi nhiều khoảng trắng hoặc dấu - liên tiếp thành một dấu -
        str = Regex.Replace(str, @"[\s-]+", "-").Trim('-');

        return str;
    }

    /// <summary>
    /// Bỏ dấu tiếng Việt bằng Unicode normalization, loại NonSpacingMark rồi xử lý riêng ký tự đ/Đ.
    /// </summary>
    /// <param name="input">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    private static string RemoveVietnameseDiacritics(string input)
    {
        // Normalize to decomposed form (NFD)
        string normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        // Replace đặc biệt
        string result = sb.ToString()
            .Replace('đ', 'd')
            .Replace('Đ', 'D');

        return result.Normalize(NormalizationForm.FormC); // Optional
    }


    /// <summary>
    /// Kiểm tra username chỉ gồm chữ/số, độ dài 6-30 ký tự.
    /// </summary>
    /// <param name="input">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public static bool IsValidUsername(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var regex = new Regex(@"^[a-zA-Z0-9]{6,30}$");
        return regex.IsMatch(input);
    }

    /// <summary>
    /// Kiểm tra mã số thuế Việt Nam dạng 10 số hoặc 10 số kèm nhánh -xxx.
    /// </summary>
    /// <param name="taxCode">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public static bool IsValidTaxCode(string taxCode)
    {
        taxCode = taxCode.Trim();
        var regex = new System.Text.RegularExpressions.Regex(@"^\d{10}(-\d{3})?$");
        return regex.IsMatch(taxCode);
    }

    /// <summary>
    /// Kiểm tra số định danh/CCCD theo định dạng số đã cấu hình trong hệ thống.
    /// </summary>
    /// <param name="identityNumber">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public static bool IsValidIdentityNumber(string identityNumber)
    {
        identityNumber = identityNumber.Trim();
        var regex = new System.Text.RegularExpressions.Regex(@"^\d{12}(-\d{3})?$");
        return regex.IsMatch(identityNumber);
    }
}
