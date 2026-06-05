using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Backend.Share.Extensions;

public static class StringExtensions
{
    public static bool IsNullOrEmpty(this string? str) =>
        string.IsNullOrEmpty(str);

    public static string Truncate(this string str, int maxLength)
    {
        if (string.IsNullOrEmpty(str)) return str;
        return str.Length <= maxLength ? str : str.Substring(0, maxLength) + "...";
    }
    public static string ToHash256(this string input)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashedBytes);
    }
    public static string RemoveVietnamese(this string text)
    {
        string[] arr1 = new string[] { "á", "à", "ả", "ã", "ạ", "â", "ấ", "ầ", "ẩ", "ẫ", "ậ", "ă", "ắ", "ằ", "ẳ", "ẵ", "ặ",
            "đ",
            "é","è","ẻ","ẽ","ẹ","ê","ế","ề","ể","ễ","ệ",
            "í","ì","ỉ","ĩ","ị",
            "ó","ò","ỏ","õ","ọ","ô","ố","ồ","ổ","ỗ","ộ","ơ","ớ","ờ","ở","ỡ","ợ",
            "ú","ù","ủ","ũ","ụ","ư","ứ","ừ","ử","ữ","ự",
            "ý","ỳ","ỷ","ỹ","ỵ",};
        string[] arr2 = new string[] { "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a",
            "d",
            "e","e","e","e","e","e","e","e","e","e","e",
            "i","i","i","i","i",
            "o","o","o","o","o","o","o","o","o","o","o","o","o","o","o","o","o",
            "u","u","u","u","u","u","u","u","u","u","u",
            "y","y","y","y","y",};
        for (int i = 0; i < arr1.Length; i++)
        {
            text = text.Replace(arr1[i], arr2[i]);
            text = text.Replace(arr1[i].ToUpper(), arr2[i].ToUpper());
        }
        return text;
    }
    public static string ToSEO(this string title)
    {
        var maxLength = title.Length;
        var match = Regex.Match(title.ToLower(), "[\\w]+");
        StringBuilder result = new StringBuilder("");
        bool maxLengthHit = false;
        while (match.Success && !maxLengthHit)
        {
            if (result.Length + match.Value.Length <= maxLength)
            {
                result.Append(match.Value + "-");
            }
            else
            {
                maxLengthHit = true;
                // Handle a situation where there is only one word and it is greater than the max length.
                if (result.Length == 0) result.Append(match.Value.Substring(0, maxLength));
            }
            match = match.NextMatch();
        }
        // Remove trailing '-'
        if (result[result.Length - 1] == '-') result.Remove(result.Length - 1, 1);
        return result.ToString();
    }
}
