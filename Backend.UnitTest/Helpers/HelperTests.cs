using System;
using Backend.Share.Helpers;
using FluentAssertions;

namespace Backend.UnitTest.Helpers;

/// <summary>
/// Unit tests cho Share/Helpers — hàm pure, không cần mock, chạy nhanh nhất.
/// Đây là loại test đơn giản nhất và nên có coverage cao nhất.
/// </summary>

// ════════════════════════════════════════════════════════════════════════════
// PasswordHelper
// ════════════════════════════════════════════════════════════════════════════
public class PasswordHelperTests
{
    [Fact]
    [Trait("Helper", "Password")]
    public void HashPassword_ProducesNonEmptyHash()
    {
        var hash = PasswordHelper.HashPassword("MySecret@123");
        hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Helper", "Password")]
    public void HashPassword_SameInput_ProducesDifferentHashEachTime()
    {
        // BCrypt dùng salt ngẫu nhiên — hai lần hash cùng password cho ra khác nhau
        var h1 = PasswordHelper.HashPassword("MySecret@123");
        var h2 = PasswordHelper.HashPassword("MySecret@123");
        h1.Should().NotBe(h2);
    }

    [Fact]
    [Trait("Helper", "Password")]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var password = "Test@12345";
        var hash = PasswordHelper.HashPassword(password);

        PasswordHelper.VerifyPassword(password, hash).Should().BeTrue();
    }

    [Fact]
    [Trait("Helper", "Password")]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var hash = PasswordHelper.HashPassword("CorrectPass@1");

        PasswordHelper.VerifyPassword("WrongPass@1", hash).Should().BeFalse();
    }

    [Fact]
    [Trait("Helper", "Password")]
    public void VerifyPassword_EmptyInput_ReturnsFalse()
    {
        var hash = PasswordHelper.HashPassword("Test@12345");

        PasswordHelper.VerifyPassword("", hash).Should().BeFalse();
    }
}

// ════════════════════════════════════════════════════════════════════════════
// EmailHelper
// ════════════════════════════════════════════════════════════════════════════
public class EmailHelperTests
{
    [Theory]
    [Trait("Helper", "Email")]
    [InlineData("user@example.com", true)]
    [InlineData("user.name+tag@sub.org", true)]
    [InlineData("user@domain.co.vn", true)]
    [InlineData("notanemail", false)]
    [InlineData("missing@", false)]
    [InlineData("@nodomain.com", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsValidEmail_VariousInputs_ReturnsExpected(string email, bool expected)
    {
        EmailHelper.IsValidEmail(email).Should().Be(expected);
    }
}

// ════════════════════════════════════════════════════════════════════════════
// PhoneHelper
// ════════════════════════════════════════════════════════════════════════════
public class PhoneHelperTests
{
    [Theory]
    [Trait("Helper", "Phone")]
    [InlineData("0912345678", true)]   // đầu 09 hợp lệ
    [InlineData("0812345678", true)]   // đầu 08 hợp lệ
    [InlineData("0712345678", true)]   // đầu 07 hợp lệ
    [InlineData("0512345678", true)]   // đầu 05 hợp lệ
    [InlineData("0312345678", true)]   // đầu 03 hợp lệ
    [InlineData("0112345678", false)]  // đầu số không hợp lệ
    [InlineData("01234567", false)]  // thiếu số
    [InlineData("09123456789", false)]  // thừa số
    [InlineData("09abcdefgh", false)]  // không phải số
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsValidVietnamPhone_VariousInputs_ReturnsExpected(string phone, bool expected)
    {
        PhoneHelper.IsValidVietnamPhone(phone).Should().Be(expected);
    }
}

// ════════════════════════════════════════════════════════════════════════════
// StringHelper
// ════════════════════════════════════════════════════════════════════════════
public class StringHelperTests
{
    // ── IsValidUsername ──────────────────────────────────────────────────────
    [Theory]
    [Trait("Helper", "String")]
    [InlineData("validuser", true)]
    [InlineData("User123", true)]
    [InlineData("abc123456", true)]
    [InlineData("ab", false)]  // quá ngắn (< 6)
    [InlineData("has space", false)]  // khoảng trắng
    [InlineData("has-dash", false)]  // ký tự không hợp lệ
    [InlineData("có_dấu", false)]  // tiếng Việt
    [InlineData("", false)]
    public void IsValidUsername_VariousInputs_ReturnsExpected(string input, bool expected)
    {
        StringHelper.IsValidUsername(input).Should().Be(expected);
    }

    // ── IsValidIdentityNumber ────────────────────────────────────────────────
    [Theory]
    [Trait("Helper", "String")]
    [InlineData("123456789012", true)]   // 12 chữ số hợp lệ
    [InlineData("12345678901", false)]  // 11 số — thiếu
    [InlineData("1234567890123", false)]  // 13 số — thừa
    [InlineData("12345678901a", false)]  // có chữ
    [InlineData("", false)]
    public void IsValidIdentityNumber_VariousInputs_ReturnsExpected(string input, bool expected)
    {
        StringHelper.IsValidIdentityNumber(input).Should().Be(expected);
    }

    // ── Slugify ──────────────────────────────────────────────────────────────
    [Theory]
    [Trait("Helper", "String")]
    [InlineData("Hello World", "hello-world")]
    [InlineData("Tiêu đề bài viết", "tieu-de-bai-viet")]
    [InlineData("  multiple   spaces  ", "multiple-spaces")]
    [InlineData("Special!!!Chars###", "specialchars")]
    [InlineData("", "")]
    public void Slugify_VariousInputs_ReturnsExpectedSlug(string input, string expected)
    {
        StringHelper.Slugify(input).Should().Be(expected);
    }

    // ── IsValidTaxCode ───────────────────────────────────────────────────────
    [Theory]
    [Trait("Helper", "String")]
    [InlineData("1234567890", true)]   // 10 số
    [InlineData("1234567890-123", true)]   // 10-3 format
    [InlineData("12345678", false)]  // thiếu số
    [InlineData("12345678901", false)]  // 11 số
    [InlineData("", false)]
    public void IsValidTaxCode_VariousInputs_ReturnsExpected(string input, bool expected)
    {
        StringHelper.IsValidTaxCode(input).Should().Be(expected);
    }
}
