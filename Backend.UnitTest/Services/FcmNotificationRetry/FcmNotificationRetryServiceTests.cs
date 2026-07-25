using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.BackgroundJobs.FcmNotificationRetry;
using Backend.Application.Constants;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Services;
using Backend.Share.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Backend.UnitTest.Services.FcmNotificationRetry;

public class FcmNotificationRetryServiceTests
{
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IFcmClient> _fcmClientMock = new();
    private readonly Mock<ILogger<FcmNotificationRetryService>> _loggerMock = new();
    private readonly IFcmFailureClassifier _classifier = new FcmFailureClassifier();

    private BackendContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BackendContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new BackendContext(options);
    }

    private Backend.Domain.Entities.User CreateTestUser()
    {
        return new Backend.Domain.Entities.User
        {
            Id = 1,
            Username = "test",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "dummy-hash"
        };
    }

    [Fact]
    public async Task RunRetryJobAsync_Should_Skip_If_Lock_Exists()
    {
        // Arrange
        using var context = CreateContext();
        _cacheMock.Setup(x => x.ExistsAsync(FcmNotificationRetryConstants.Job.LockKeyPrefix))
            .ReturnsAsync(true);

        var service = new FcmNotificationRetryService(context, _classifier, _cacheMock.Object, _fcmClientMock.Object, _loggerMock.Object);

        // Act
        var result = await service.RunRetryJobAsync(CancellationToken.None);

        // Assert
        result.SkippedByLock.Should().BeTrue();
        _fcmClientMock.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunRetryJobAsync_Should_Query_Only_Eligible_Logs()
    {
        // Arrange
        using var context = CreateContext();
        var now = DateTime.UtcNow;

        var user = CreateTestUser();
        var device = new UserDevice { Id = 10, UserId = 1, DeviceToken = "token1", IsDeleted = false };
        context.Users.Add(user);
        context.UserDevices.Add(device);

        // 1. Unsent Pending log - Eligible
        var log1 = new FcmNotificationLog
        {
            Id = 1, UserId = 1, UserDeviceId = 10, Title = "L1", Body = "B1",
            IsSent = false, IsDeleted = false, Status = FcmNotificationRetryConstants.Status.Pending,
            AttemptCount = 0, NextRetryAt = null, CreatedDate = now.AddMinutes(-5)
        };

        // 2. Already sent log - Ineligible
        var log2 = new FcmNotificationLog
        {
            Id = 2, UserId = 1, UserDeviceId = 10, Title = "L2", Body = "B2",
            IsSent = true, IsDeleted = false, Status = FcmNotificationRetryConstants.Status.Sent,
            AttemptCount = 1, NextRetryAt = null, CreatedDate = now.AddMinutes(-5)
        };

        // 3. Soft-deleted log - Ineligible
        var log3 = new FcmNotificationLog
        {
            Id = 3, UserId = 1, UserDeviceId = 10, Title = "L3", Body = "B3",
            IsSent = false, IsDeleted = true, Status = FcmNotificationRetryConstants.Status.Pending,
            AttemptCount = 0, NextRetryAt = null, CreatedDate = now.AddMinutes(-5)
        };

        // 4. Exhausted log - Ineligible
        var log4 = new FcmNotificationLog
        {
            Id = 4, UserId = 1, UserDeviceId = 10, Title = "L4", Body = "B4",
            IsSent = false, IsDeleted = false, Status = FcmNotificationRetryConstants.Status.Exhausted,
            AttemptCount = 3, NextRetryAt = null, CreatedDate = now.AddMinutes(-5)
        };

        // 5. Future retry log - Ineligible
        var log5 = new FcmNotificationLog
        {
            Id = 5, UserId = 1, UserDeviceId = 10, Title = "L5", Body = "B5",
            IsSent = false, IsDeleted = false, Status = FcmNotificationRetryConstants.Status.RetryScheduled,
            AttemptCount = 1, NextRetryAt = now.AddMinutes(5), CreatedDate = now.AddMinutes(-5)
        };

        context.FcmNotificationLogs.AddRange(log1, log2, log3, log4, log5);
        await context.SaveChangesAsync();

        _cacheMock.Setup(x => x.ExistsAsync(FcmNotificationRetryConstants.Job.LockKeyPrefix)).ReturnsAsync(false);
        _fcmClientMock.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-id-123");

        var service = new FcmNotificationRetryService(context, _classifier, _cacheMock.Object, _fcmClientMock.Object, _loggerMock.Object);

        // Act
        var result = await service.RunRetryJobAsync(CancellationToken.None);

        // Assert
        result.Eligible.Should().Be(1);
        result.Claimed.Should().Be(1);
        result.Sent.Should().Be(1);

        var dbLog1 = await context.FcmNotificationLogs.FindAsync(1);
        dbLog1!.Status.Should().Be(FcmNotificationRetryConstants.Status.Sent);
        dbLog1.IsSent.Should().BeTrue();
        dbLog1.ProviderMessageId.Should().Be("msg-id-123");
        dbLog1.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task RunRetryJobAsync_Should_Recover_Stuck_Lease_Logs()
    {
        // Arrange
        using var context = CreateContext();
        var now = DateTime.UtcNow;

        var user = CreateTestUser();
        var device = new UserDevice { Id = 10, UserId = 1, DeviceToken = "token1", IsDeleted = false };
        context.Users.Add(user);
        context.UserDevices.Add(device);

        // Stuck log (PROCESSING with expired lease)
        var stuckLog = new FcmNotificationLog
        {
            Id = 1, UserId = 1, UserDeviceId = 10, Title = "L1", Body = "B1",
            IsSent = false, IsDeleted = false, Status = FcmNotificationRetryConstants.Status.Processing,
            AttemptCount = 1, NextRetryAt = null, ProcessingLeaseUntil = now.AddMinutes(-1),
            ProcessingBy = "expired-worker", CreatedDate = now.AddMinutes(-10)
        };

        context.FcmNotificationLogs.Add(stuckLog);
        await context.SaveChangesAsync();

        _cacheMock.Setup(x => x.ExistsAsync(FcmNotificationRetryConstants.Job.LockKeyPrefix)).ReturnsAsync(false);
        _fcmClientMock.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-recovered");

        var service = new FcmNotificationRetryService(context, _classifier, _cacheMock.Object, _fcmClientMock.Object, _loggerMock.Object);

        // Act
        var result = await service.RunRetryJobAsync(CancellationToken.None);

        // Assert
        result.LeaseRecovered.Should().Be(1);
        result.Eligible.Should().Be(1); // Eligible on the same run after recovery
        result.Sent.Should().Be(1);

        var dbLog = await context.FcmNotificationLogs.FindAsync(1);
        dbLog!.Status.Should().Be(FcmNotificationRetryConstants.Status.Sent);
        dbLog.IsSent.Should().BeTrue();
    }

    [Fact]
    public async Task RunRetryJobAsync_Transient_Failure_Should_Increment_Attempts_And_Calculate_Backoff()
    {
        // Arrange
        using var context = CreateContext();
        var now = DateTime.UtcNow;

        var user = CreateTestUser();
        var device = new UserDevice { Id = 10, UserId = 1, DeviceToken = "token1", IsDeleted = false };
        context.Users.Add(user);
        context.UserDevices.Add(device);

        var log = new FcmNotificationLog
        {
            Id = 1, UserId = 1, UserDeviceId = 10, Title = "L1", Body = "B1",
            IsSent = false, IsDeleted = false, Status = FcmNotificationRetryConstants.Status.Pending,
            AttemptCount = 0, NextRetryAt = null, CreatedDate = now.AddMinutes(-5)
        };

        context.FcmNotificationLogs.Add(log);
        await context.SaveChangesAsync();

        _cacheMock.Setup(x => x.ExistsAsync(FcmNotificationRetryConstants.Job.LockKeyPrefix)).ReturnsAsync(false);

        // Simulate transient FCM connection exception
        var transientException = new Exception("Connection reset by peer / temporary DNS issue");
        _fcmClientMock.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(transientException);

        var service = new FcmNotificationRetryService(context, _classifier, _cacheMock.Object, _fcmClientMock.Object, _loggerMock.Object);

        // Act
        var result = await service.RunRetryJobAsync(CancellationToken.None);

        // Assert
        result.RetryScheduled.Should().Be(1);
        result.Sent.Should().Be(0);

        var dbLog = await context.FcmNotificationLogs.FindAsync(1);
        dbLog!.Status.Should().Be(FcmNotificationRetryConstants.Status.RetryScheduled);
        dbLog.AttemptCount.Should().Be(1);
        dbLog.NextRetryAt.Should().NotBeNull();
        dbLog.NextRetryAt.Value.Should().BeAfter(now);
        dbLog.ErrorMessage.Should().Contain("Connection reset");
        dbLog.LastErrorCode.Should().Be("Exception");
    }

    [Fact]
    public async Task RunRetryJobAsync_Exhausted_Attempts_Should_Mark_Exhausted()
    {
        // Arrange
        using var context = CreateContext();
        var now = DateTime.UtcNow;

        var user = CreateTestUser();
        var device = new UserDevice { Id = 10, UserId = 1, DeviceToken = "token1", IsDeleted = false };
        context.Users.Add(user);
        context.UserDevices.Add(device);

        // Already had 2 attempts. Next failed attempt (3rd) should mark exhausted
        var log = new FcmNotificationLog
        {
            Id = 1, UserId = 1, UserDeviceId = 10, Title = "L1", Body = "B1",
            IsSent = false, IsDeleted = false, Status = FcmNotificationRetryConstants.Status.RetryScheduled,
            AttemptCount = 2, NextRetryAt = null, CreatedDate = now.AddMinutes(-5)
        };

        context.FcmNotificationLogs.Add(log);
        await context.SaveChangesAsync();

        _cacheMock.Setup(x => x.ExistsAsync(FcmNotificationRetryConstants.Job.LockKeyPrefix)).ReturnsAsync(false);

        var transientException = new Exception("Unavailable FCM Service");
        _fcmClientMock.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(transientException);

        var service = new FcmNotificationRetryService(context, _classifier, _cacheMock.Object, _fcmClientMock.Object, _loggerMock.Object);

        // Act
        var result = await service.RunRetryJobAsync(CancellationToken.None);

        // Assert
        result.Exhausted.Should().Be(1);
        result.PermanentFailed.Should().Be(1);

        var dbLog = await context.FcmNotificationLogs.FindAsync(1);
        dbLog!.Status.Should().Be(FcmNotificationRetryConstants.Status.Exhausted);
        dbLog.AttemptCount.Should().Be(3);
        dbLog.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public async Task RunRetryJobAsync_InvalidToken_Should_Deactivate_UserDevice_And_Mark_Failed_Permanent()
    {
        // Arrange
        using var context = CreateContext();
        var now = DateTime.UtcNow;

        var user = CreateTestUser();
        var device = new UserDevice { Id = 10, UserId = 1, DeviceToken = "token1", IsDeleted = false };
        context.Users.Add(user);
        context.UserDevices.Add(device);

        var log = new FcmNotificationLog
        {
            Id = 1, UserId = 1, UserDeviceId = 10, Title = "L1", Body = "B1",
            IsSent = false, IsDeleted = false, Status = FcmNotificationRetryConstants.Status.Pending,
            AttemptCount = 0, NextRetryAt = null, CreatedDate = now.AddMinutes(-5)
        };

        context.FcmNotificationLogs.Add(log);
        await context.SaveChangesAsync();

        _cacheMock.Setup(x => x.ExistsAsync(FcmNotificationRetryConstants.Job.LockKeyPrefix)).ReturnsAsync(false);

        // Simulate invalid/unregistered token exception
        var unregisteredException = new Exception("FCM response: registration token not registered or invalid token");
        _fcmClientMock.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(unregisteredException);

        var service = new FcmNotificationRetryService(context, _classifier, _cacheMock.Object, _fcmClientMock.Object, _loggerMock.Object);

        // Act
        var result = await service.RunRetryJobAsync(CancellationToken.None);

        // Assert
        result.InvalidTokens.Should().Be(1);
        result.DevicesRevoked.Should().Be(1);

        var dbLog = await context.FcmNotificationLogs.FindAsync(1);
        dbLog!.Status.Should().Be(FcmNotificationRetryConstants.Status.FailedPermanent);
        dbLog.IsDeleted.Should().BeTrue(); // Soft-deleted log according to FDS baseline

        var dbDevice = await context.UserDevices.FindAsync(10);
        dbDevice!.IsDeleted.Should().BeTrue(); // Deactivated token
    }

    [Fact]
    public async Task RunRetryJobAsync_GlobalConfigError_Should_Pause_Execution_Without_Deactivating_Tokens()
    {
        // Arrange
        using var context = CreateContext();
        var now = DateTime.UtcNow;

        var user = CreateTestUser();
        var device = new UserDevice { Id = 10, UserId = 1, DeviceToken = "token1", IsDeleted = false };
        context.Users.Add(user);
        context.UserDevices.Add(device);

        var log = new FcmNotificationLog
        {
            Id = 1, UserId = 1, UserDeviceId = 10, Title = "L1", Body = "B1",
            IsSent = false, IsDeleted = false, Status = FcmNotificationRetryConstants.Status.Pending,
            AttemptCount = 0, NextRetryAt = null, CreatedDate = now.AddMinutes(-5)
        };

        context.FcmNotificationLogs.Add(log);
        await context.SaveChangesAsync();

        _cacheMock.Setup(x => x.ExistsAsync(FcmNotificationRetryConstants.Job.LockKeyPrefix)).ReturnsAsync(false);

        // Simulate global project config error (e.g. invalid credential or permission denied)
        var globalException = new Exception("Firebase permission denied or invalid credentials JSON file");
        _fcmClientMock.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(globalException);

        var service = new FcmNotificationRetryService(context, _classifier, _cacheMock.Object, _fcmClientMock.Object, _loggerMock.Object);

        // Act
        var result = await service.RunRetryJobAsync(CancellationToken.None);

        // Assert
        result.Processed.Should().Be(1);

        var dbLog = await context.FcmNotificationLogs.FindAsync(1);
        dbLog!.Status.Should().Be(FcmNotificationRetryConstants.Status.RetryScheduled);
        dbLog.AttemptCount.Should().Be(0); // AttemptCount is rolled back, not incremented for global configuration errors
        dbLog.NextRetryAt.Should().BeCloseTo(now.AddMinutes(5), TimeSpan.FromSeconds(10)); // Paused next retry

        var dbDevice = await context.UserDevices.FindAsync(10);
        dbDevice!.IsDeleted.Should().BeFalse(); // Device not deactivated
    }

    [Fact]
    public async Task RunRetryJobAsync_Stale_Notification_With_Resolved_Alert_Should_Be_Marked_Stale_And_Skipped()
    {
        // Arrange
        using var context = CreateContext();
        var now = DateTime.UtcNow;

        var user = CreateTestUser();
        var device = new UserDevice { Id = 10, UserId = 1, DeviceToken = "token1", IsDeleted = false };
        context.Users.Add(user);
        context.UserDevices.Add(device);

        // Active resolved alert
        var alert = new Alert
        {
            Id = 55, Status = AlertConstants.Status.Resolved, AlertType = AlertConstants.Type.LowStock,
            Severity = AlertConstants.Severity.Warning, Message = "A1", IsDeleted = false
        };
        context.Alerts.Add(alert);

        var log = new FcmNotificationLog
        {
            Id = 1, UserId = 1, UserDeviceId = 10, Title = "L1", Body = "B1",
            IsSent = false, IsDeleted = false, Status = FcmNotificationRetryConstants.Status.Pending,
            AttemptCount = 0, NextRetryAt = null, CreatedDate = now.AddMinutes(-5),
            ReferenceType = "ALERT", ReferenceId = 55
        };

        context.FcmNotificationLogs.Add(log);
        await context.SaveChangesAsync();

        _cacheMock.Setup(x => x.ExistsAsync(FcmNotificationRetryConstants.Job.LockKeyPrefix)).ReturnsAsync(false);

        var service = new FcmNotificationRetryService(context, _classifier, _cacheMock.Object, _fcmClientMock.Object, _loggerMock.Object);

        // Act
        var result = await service.RunRetryJobAsync(CancellationToken.None);

        // Assert
        result.Stale.Should().Be(1);
        _fcmClientMock.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);

        var dbLog = await context.FcmNotificationLogs.FindAsync(1);
        dbLog!.Status.Should().Be(FcmNotificationRetryConstants.Status.Stale);
        dbLog.LastErrorCode.Should().Be("STALE");
    }

    [Fact]
    public async Task RunRetryJobAsync_Malformed_Payload_Should_Be_Failed_Permanent_And_Not_Retry()
    {
        // Arrange
        using var context = CreateContext();
        var now = DateTime.UtcNow;

        var user = CreateTestUser();
        var device = new UserDevice { Id = 10, UserId = 1, DeviceToken = "token1", IsDeleted = false };
        context.Users.Add(user);
        context.UserDevices.Add(device);

        var log = new FcmNotificationLog
        {
            Id = 1, UserId = 1, UserDeviceId = 10, Title = "L1", Body = "B1",
            IsSent = false, IsDeleted = false, Status = FcmNotificationRetryConstants.Status.Pending,
            AttemptCount = 0, NextRetryAt = null, CreatedDate = now.AddMinutes(-5),
            DataPayload = "invalid-malformed-json"
        };

        context.FcmNotificationLogs.Add(log);
        await context.SaveChangesAsync();

        _cacheMock.Setup(x => x.ExistsAsync(FcmNotificationRetryConstants.Job.LockKeyPrefix)).ReturnsAsync(false);

        var service = new FcmNotificationRetryService(context, _classifier, _cacheMock.Object, _fcmClientMock.Object, _loggerMock.Object);

        // Act
        var result = await service.RunRetryJobAsync(CancellationToken.None);

        // Assert
        result.PayloadInvalid.Should().Be(1);
        result.PermanentFailed.Should().Be(1);

        var dbLog = await context.FcmNotificationLogs.FindAsync(1);
        dbLog!.Status.Should().Be(FcmNotificationRetryConstants.Status.FailedPermanent);
        dbLog.LastErrorCode.Should().Be("PAYLOAD_INVALID");
    }
}
