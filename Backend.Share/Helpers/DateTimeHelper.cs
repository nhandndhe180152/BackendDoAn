using System;
using System.Runtime.InteropServices;

namespace Backend.Share.Helpers;

public static class DateTimeHelper
{
    /// <summary>
    /// Danh sách timezone id có thể dùng để lấy múi giờ Việt Nam.
    /// 
    /// Trên Linux/macOS/Docker thường dùng IANA timezone:
    /// - Asia/Ho_Chi_Minh
    /// 
    /// Trên Windows thường dùng Windows timezone:
    /// - SE Asia Standard Time
    /// 
    /// Viết dạng danh sách fallback để code chạy được trên nhiều nền tảng.
    /// </summary>
    private static readonly string[] VietnamTimeZoneIds =
    {
        "Asia/Ho_Chi_Minh",
        "SE Asia Standard Time"
    };

    /// <summary>
    /// Lấy thời gian hiện tại theo giờ Việt Nam.
    /// 
    /// Luồng xử lý:
    /// 1. Lấy thời gian UTC hiện tại bằng DateTime.UtcNow.
    /// 2. Tìm timezone Việt Nam phù hợp với nền tảng đang chạy.
    /// 3. Convert UTC sang giờ Việt Nam.
    /// 
    /// Không dùng DateTime.Now vì nó phụ thuộc timezone máy chủ.
    /// </summary>
    /// <returns>Thời gian hiện tại theo múi giờ Việt Nam</returns>
    public static DateTime VietnamNow()
    {
        var vietnamTimeZone = GetVietnamTimeZone();

        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);
    }

    /// <summary>
    /// Lấy thông tin timezone Việt Nam theo cơ chế fallback.
    /// 
    /// Vì Windows và Linux/macOS dùng timezone id khác nhau,
    /// nên hàm này sẽ thử lần lượt các timezone id trong VietnamTimeZoneIds.
    /// 
    /// Nếu chạy trên Linux/macOS/Docker:
    /// - "Asia/Ho_Chi_Minh" thường sẽ chạy được.
    /// 
    /// Nếu chạy trên Windows:
    /// - "SE Asia Standard Time" thường sẽ chạy được.
    /// 
    /// Nếu không tìm được timezone nào, hàm sẽ fallback thủ công về UTC+7.
    /// </summary>
    /// <returns>TimeZoneInfo của Việt Nam</returns>
    private static TimeZoneInfo GetVietnamTimeZone()
    {
        foreach (var timeZoneId in VietnamTimeZoneIds)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                // Timezone id không tồn tại trên hệ điều hành hiện tại.
                // Ví dụ Windows có thể không hiểu "Asia/Ho_Chi_Minh".
                // Bỏ qua và thử timezone id tiếp theo.
            }
            catch (InvalidTimeZoneException)
            {
                // Timezone tồn tại nhưng dữ liệu timezone bị lỗi.
                // Bỏ qua và thử timezone id tiếp theo.
            }
        }

        // Fallback cuối cùng nếu cả Windows timezone và IANA timezone đều không dùng được.
        // Việt Nam không dùng daylight saving time, nên UTC+7 là ổn định.
        return TimeZoneInfo.CreateCustomTimeZone(
            id: "Vietnam Standard Time Custom",
            baseUtcOffset: TimeSpan.FromHours(7),
            displayName: "(UTC+07:00) Vietnam Time",
            standardDisplayName: "Vietnam Time"
        );
    }
}
