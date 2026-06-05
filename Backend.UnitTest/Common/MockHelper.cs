using System;
using Backend.Application.DependencyInjection.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Backend.UnitTest.Common;

/// <summary>
/// Factory tạo các mock object dùng chung cho toàn bộ test suite.
/// Tránh lặp code setup ở mỗi test class.
/// </summary>
public static class MockHelper
{
    // ── Logger ──────────────────────────────────────────────────────────────
    public static Mock<ILoggerFactory> LoggerFactory()
    {
        var mockFactory = new Mock<ILoggerFactory>();
        mockFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        return mockFactory;
    }

    // ── Options / Settings ───────────────────────────────────────────────────
    public static IOptions<HostSettings> HostSettings(
        string adminUrl = "https://admin.test.local",
        string clientUrl = "https://client.test.local",
        string apiUrl = "https://api.test.local")
    {
        return Options.Create(new HostSettings
        {
            AdminUrl = adminUrl,
            ClientUrl = clientUrl,
            ApiUrl = apiUrl
        });
    }

    // ── HttpContext ──────────────────────────────────────────────────────────
    public static Mock<IHttpContextAccessor> HttpContextAccessor(int userId = 1)
    {
        var mockAccessor = new Mock<IHttpContextAccessor>();
        var mockContext = new Mock<HttpContext>();
        mockAccessor.Setup(a => a.HttpContext).Returns(mockContext.Object);
        return mockAccessor;
    }
}
