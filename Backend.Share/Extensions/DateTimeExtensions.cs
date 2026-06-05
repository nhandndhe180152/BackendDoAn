using System;

namespace Backend.Share.Extensions;

public static class DateTimeExtensions
{
    public static string ToVietnameseDate(this DateTime dateTime)
    {
        return dateTime.ToString("dd/MM/yyyy");
    }
    public static string ToVietnameseDateOffset(this DateTime dateTime)
    {
        return dateTime.ToString("dd/MM/yyyy");
    }

    public static string ToVietnameseDateTime(this DateTime dateTime)
    {
        return dateTime.ToString("dd/MM/yyyy HH:mm:ss");
    }

    public static string ToVietnameseDateTimeOffset(this DateTimeOffset dateTime)
    {
        return dateTime.ToString("dd/MM/yyyy HH:mm:ss");
    }

    public static (DateTime startDate, DateTime endDate) GetDateRange(string period)
    {
        var now = DateTime.Now;
        return period switch
        {
            "TODAY" =>
            (
                now.Date,
                now.Date.AddDays(1).AddTicks(-1)
            ),

            "WEEK" =>
            (
                // Thứ 2 tuần hiện tại
                now.Date.AddDays(-(int)(now.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)now.DayOfWeek - 1)),
                // Chủ nhật tuần hiện tại (cuối ngày)
                now.Date.AddDays(-(int)(now.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)now.DayOfWeek - 1))
                    .AddDays(7).AddTicks(-1)
            ),

            "MONTH" =>
            (
                new DateTime(now.Year, now.Month, 1),
                new DateTime(now.Year, now.Month, 1).AddMonths(1).AddTicks(-1)
            ),

            "YEAR" =>
            (
                new DateTime(now.Year, 1, 1),
                new DateTime(now.Year + 1, 1, 1).AddTicks(-1)
            ),

            _ =>
            (
                now.AddDays(-6).Date,
                now.Date.AddDays(1).AddTicks(-1)
            )
        };
    }
    public static string FormatTimeSpan(TimeSpan ts)
    {
        var totalHours = ts.TotalHours;

        if (totalHours < 1)
        {
            // Dưới 1 giờ thì hiện phút
            return $"{ts.Minutes}p";
        }
        else if (totalHours < 24)
        {
            // Dưới 24 giờ thì hiện giờ:phút
            var hours = (int)totalHours;
            var minutes = ts.Minutes;
            return minutes > 0 ? $"{hours}h{minutes:00}p" : $"{hours}h";
        }
        else
        {
            // Trên 24 giờ thì hiện ngày và giờ
            var days = ts.Days;
            var hours = ts.Hours;
            return hours > 0 ? $"{days}d{hours}h" : $"{days}d";
        }
    }
}
