using System;
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

        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Basic ")) return Unauthorized(httpContext);

        var encoded = header["Basic ".Length..].Trim();
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        var parts = decoded.Split(':');

        if (parts.Length != 2) return Unauthorized(httpContext);

        var username = parts[0];
        var password = parts[1];

        return username == _username && password == _password;
    }

    private static bool Unauthorized(HttpContext context)
    {
        context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Hangfire Dashboard\"";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return false;
    }
}
