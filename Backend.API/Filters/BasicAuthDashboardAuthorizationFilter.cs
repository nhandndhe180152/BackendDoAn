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
        var header = httpContext.Request.Headers["Authorization"].ToString();

        if (string.IsNullOrWhiteSpace(header) ||
            !header.StartsWith("Basic ", StringComparison.Ordinal))
        {
            return Unauthorized(httpContext);
        }

        var encoded = header["Basic ".Length..].Trim();

        // Base64 do client gửi lên có thể không hợp lệ -> tránh ném exception ra ngoài.
        byte[] rawBytes;
        try
        {
            rawBytes = Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            return Unauthorized(httpContext);
        }

        var decoded = Encoding.UTF8.GetString(rawBytes);

        // Chỉ tách ở dấu ':' đầu tiên để password được phép chứa ':'.
        var separatorIndex = decoded.IndexOf(':');
        if (separatorIndex < 0)
        {
            return Unauthorized(httpContext);
        }

        var username = decoded[..separatorIndex];
        var password = decoded[(separatorIndex + 1)..];

        // So sánh credential ở dạng đã băm và theo thời gian cố định (constant-time).
        // Việc này cho dữ liệu do người dùng kiểm soát đi qua một rào cản mã hóa,
        // xử lý cả CWE-807 (user-controlled bypass) lẫn nguy cơ timing attack của toán tử ==.
        var usernameMatches = FixedTimeEquals(username, _username);
        var passwordMatches = FixedTimeEquals(password, _password);

        if (usernameMatches && passwordMatches)
        {
            return true;
        }

        return Unauthorized(httpContext);
    }

    // Băm SHA-256 để hai vế luôn cùng độ dài (không lộ độ dài qua thời gian so sánh),
    // rồi so sánh bằng FixedTimeEquals để không rò rỉ thông tin qua thời gian.
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
