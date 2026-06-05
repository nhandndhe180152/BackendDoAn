using System;

namespace Backend.Share.Helpers;

public static class CronHelper
{
    /// <summary>
    /// Chạy mỗi n phút. VD: EveryMinute(5) => mỗi 5 phút
    /// </summary>
    public static string EveryMinute(int interval = 1)
    {
        return $"*/{interval} * * * *";
    }

    /// <summary>
    /// Chạy mỗi n giờ. VD: EveryHour(2) => mỗi 2 giờ
    /// </summary>
    public static string EveryHour(int interval = 1)
    {
        return $"0 */{interval} * * *";
    }

    /// <summary>
    /// Chạy vào giờ cố định mỗi ngày. VD: DailyAt(2, 30) => 2:30 sáng mỗi ngày
    /// </summary>
    public static string DailyAt(int hour, int minute = 0)
    {
        return $"{minute} {hour} * * *";
    }

    /// <summary>
    /// Chạy vào ngày cố định hàng tuần (chủ nhật = 0, thứ 2 = 1...). VD: Weekly(DayOfWeek.Monday, 3, 0) => 3:00 sáng thứ 2
    /// </summary>
    public static string Weekly(DayOfWeek day, int hour = 0, int minute = 0)
    {
        return $"{minute} {hour} * * {(int)day}";
    }

    /// <summary>
    /// Chạy vào ngày cố định hàng tháng. VD: Monthly(1, 5, 30) => 5:30 sáng ngày 1 hàng tháng
    /// </summary>
    public static string Monthly(int day = 1, int hour = 0, int minute = 0)
    {
        return $"{minute} {hour} {day} * *";
    }

    /// <summary>
    /// Chạy vào mỗi giờ đúng (ví dụ 0 phút)
    /// </summary>
    public const string Hourly = "0 * * * *";

    /// <summary>
    /// Chạy mỗi ngày lúc nửa đêm
    /// </summary>
    public const string Daily = "0 0 * * *";

    /// <summary>
    /// Tùy chỉnh Cron theo các tham số (phù hợp để gán từ config)
    /// </summary>
    public static string Build(int minute = 0, int hour = 0, string dayOfMonth = "*", string month = "*", string dayOfWeek = "*")
    {
        return $"{minute} {hour} {dayOfMonth} {month} {dayOfWeek}";
    }
}
