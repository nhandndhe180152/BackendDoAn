using System;
using Backend.Share.Extensions;
using FluentAssertions;

namespace Backend.UnitTest.Extensions;

/// <summary>
/// Unit tests cho Share/Extensions — pure functions, không cần mock.
/// </summary>

// ════════════════════════════════════════════════════════════════════════════
// DateTimeExtensions
// ════════════════════════════════════════════════════════════════════════════
public class DateTimeExtensionsTests
{
    [Fact]
    [Trait("Extension", "DateTime")]
    public void ToVietnameseDate_ReturnsCorrectFormat()
    {
        var date = new DateTime(2026, 4, 16);
        var result = date.ToVietnameseDate();

        result.Should().Be("16/04/2026");
    }

    [Fact]
    [Trait("Extension", "DateTime")]
    public void ToVietnameseDateTime_ReturnsCorrectFormat()
    {
        var dt = new DateTime(2026, 4, 16, 14, 30, 55);
        var result = dt.ToVietnameseDateTime();

        result.Should().Be("16/04/2026 14:30:55");
    }

    // ── GetDateRange ─────────────────────────────────────────────────────────

    [Fact]
    [Trait("Extension", "DateTime")]
    public void GetDateRange_Today_StartAndEndSameDay()
    {
        var (start, end) = DateTimeExtensions.GetDateRange("TODAY");

        start.Date.Should().Be(DateTime.Now.Date);
        end.Date.Should().Be(DateTime.Now.Date);
        start.Should().BeBefore(end);
    }

    [Fact]
    [Trait("Extension", "DateTime")]
    public void GetDateRange_Month_StartsOnFirstOfMonth()
    {
        var (start, _) = DateTimeExtensions.GetDateRange("MONTH");

        start.Day.Should().Be(1);
        start.Month.Should().Be(DateTime.Now.Month);
        start.Year.Should().Be(DateTime.Now.Year);
    }

    [Fact]
    [Trait("Extension", "DateTime")]
    public void GetDateRange_Year_StartsOnJanuaryFirst()
    {
        var (start, end) = DateTimeExtensions.GetDateRange("YEAR");

        start.Should().Be(new DateTime(DateTime.Now.Year, 1, 1));
        end.Year.Should().Be(DateTime.Now.Year);
    }

    [Fact]
    [Trait("Extension", "DateTime")]
    public void GetDateRange_UnknownPeriod_ReturnsLast7Days()
    {
        var (start, end) = DateTimeExtensions.GetDateRange("UNKNOWN_PERIOD");

        // Default: last 7 days (6 ngày trước đến hôm nay)
        start.Date.Should().Be(DateTime.Now.AddDays(-6).Date);
        end.Date.Should().Be(DateTime.Now.Date);
    }

    // ── FormatTimeSpan ───────────────────────────────────────────────────────

    [Theory]
    [Trait("Extension", "DateTime")]
    [InlineData(0, 30, 0, "30p")]       // 30 phút
    [InlineData(1, 0, 0, "1h")]        // 1 giờ đúng
    [InlineData(1, 30, 0, "1h30p")]     // 1 giờ 30 phút
    [InlineData(2, 0, 0, "2h")]        // 2 giờ đúng
    [InlineData(25, 0, 0, "1d1h")]      // > 24 giờ
    [InlineData(48, 0, 0, "2d")]        // 2 ngày đúng
    public void FormatTimeSpan_VariousInputs_ReturnsExpectedString(
        int hours, int minutes, int seconds, string expected)
    {
        var ts = new TimeSpan(hours, minutes, seconds);
        var result = DateTimeExtensions.FormatTimeSpan(ts);

        result.Should().Be(expected);
    }
}

// ════════════════════════════════════════════════════════════════════════════
// EnumerableExtensions
// ════════════════════════════════════════════════════════════════════════════
public class EnumerableExtensionsTests
{
    [Fact]
    [Trait("Extension", "Enumerable")]
    public void IsNullOrEmpty_NullCollection_ReturnsTrue()
    {
        List<int>? list = null;
        list.IsNullOrEmpty().Should().BeTrue();
    }

    [Fact]
    [Trait("Extension", "Enumerable")]
    public void IsNullOrEmpty_EmptyCollection_ReturnsTrue()
    {
        new List<int>().IsNullOrEmpty().Should().BeTrue();
    }

    [Fact]
    [Trait("Extension", "Enumerable")]
    public void IsNullOrEmpty_NonEmptyCollection_ReturnsFalse()
    {
        new List<int> { 1, 2, 3 }.IsNullOrEmpty().Should().BeFalse();
    }
}
