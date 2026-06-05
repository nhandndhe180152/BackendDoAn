using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;

namespace Backend.Application.DependencyInjection.Extentions;

public static class HttpContextExtensions
{
    public static string? GetRemoteHostIpAddress(this HttpContext httpContext)
    {
        IPAddress? remoteIpAddress = null;
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim());

            foreach (var ip in ips)
            {
                if (IPAddress.TryParse(ip, out var address) &&
                    (address.AddressFamily is AddressFamily.InterNetwork
                     or AddressFamily.InterNetworkV6))
                {
                    remoteIpAddress = address;
                    break;
                }
            }
        }
        else
        {
            remoteIpAddress = httpContext.Connection.RemoteIpAddress;
        }

        var ipAddress = remoteIpAddress?.ToString();

        return ipAddress == "::1" ? "127.0.0.1" : ipAddress;
    }
}