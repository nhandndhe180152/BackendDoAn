using System;
using System.Security.Cryptography;
using System.Text;
using Hangfire.Dashboard;

namespace Backend.API.Filters;

public class BasicAuthDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly string _username;
    private readonly string _password;

    public BasicAuthDashboardAuthorizationFilter(string username, string password)
    {
        _username = username;
        _password = password;
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Toàn bộ phần đọc/parse header (do người dùng kiểm soát) nằm gọn trong ParseBasicHeader
        // và KHÔNG chứa thao tác nhạy cảm nào. Nếu header sai định dạng, hàm trả về chuỗi rỗng.
        var (username, password) = ParseBasicHeader(httpContext.Request.Headers["Authorization"].ToString());

        // So sánh credential chạy vô điều kiện (không có nhánh if do người dùng kiểm soát đứng trước).
        // Dùng & (không short-circuit) để cả hai vế luôn được đánh giá -> ổn định về thời gian.
        var isAuthenticated =
            FixedTimeEquals(username, _username) & FixedTimeEquals(password, _password);

        return isAuthenticated || Unauthorized(httpContext);
    }

    // Chỉ làm nhiệm vụ tách username/password từ header Basic Auth.
    // Mọi trường hợp không hợp lệ đều trả về ("", "") thay vì ném exception hay điều khiển luồng nhạy cảm.
    private static (string Username, string Password) ParseBasicHeader(string header)
    {
        if (string.IsNullOrWhiteSpace(header) ||
            !header.StartsWith("Basic ", StringComparison.Ordinal))
        {
            return (string.Empty, string.Empty);
        }

        var encoded = header["Basic ".Length..].Trim();

        byte[] rawBytes;
        try
        {
            rawBytes = Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            return (string.Empty, string.Empty);
        }

        var decoded = Encoding.UTF8.GetString(rawBytes);

        // Tách ở dấu ':' đầu tiên để password được phép chứa ':'.
        var separatorIndex = decoded.IndexOf(':');
        if (separatorIndex < 0)
        {
            return (string.Empty, string.Empty);
        }

        return (decoded[..separatorIndex], decoded[(separatorIndex + 1)..]);
    }

    // Băm SHA-256 để hai vế luôn cùng độ dài, rồi so sánh theo thời gian cố định (constant-time)
    // nhằm chống timing attack.
    private static bool FixedTimeEquals(string left, string right)
    {
        var leftHash = SHA256.HashData(Encoding.UTF8.GetBytes(left));
        var rightHash = SHA256.HashData(Encoding.UTF8.GetBytes(right));
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }

    private static bool Unauthorized(HttpContext context)
    {
        context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Hangfire Dashboard\"";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return false;
    }
}
