using System;
using System.Linq.Expressions;
using Backend.Application.DTOs.Actions;
using Backend.Application.DTOs.NotificationTypes;
using Backend.Application.DTOs.UserStatuses;
using Backend.Application.Implements;
using Backend.Domain.Interfaces.Repositories;
using Backend.UnitTest.Common;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;

namespace Backend.UnitTest.Services;

/// <summary>
/// Tests covering GetAllAsync / GetByIdAsync / SoftDeleteAsync / CreateAsync
/// trên các service đơn giản — mỗi service nhận đúng 1 repository.
/// </summary>
public class SimpleServiceCoverageTests
{
    // ════════════════════════════════════════════════════════════════════════
    // ActionService  — ctor: (IActionRepository)
    // ════════════════════════════════════════════════════════════════════════
    [Fact][Trait("Service","Action")]
    public async Task ActionService_GetAllAsync_ReturnsSuccess()
    {
        var repo = new Mock<IActionRepository>();
        repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Backend.Domain.Entities.Action, bool>>>(), It.IsAny<bool>()))
            .Returns(Enumerable.Empty<Backend.Domain.Entities.Action>().AsQueryable().BuildMock());
        var result = await new ActionService(repo.Object).GetAllAsync();
        result.Should().NotBeNull();
    }
 
    [Fact][Trait("Service","Action")]
    public async Task ActionService_GetByIdAsync_NotFound_Returns404()
    {
        var repo = new Mock<IActionRepository>();
        repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Backend.Domain.Entities.Action, bool>>>(), It.IsAny<bool>()))
            .Returns(Enumerable.Empty<Backend.Domain.Entities.Action>().AsQueryable().BuildMock());
        var result = await new ActionService(repo.Object).GetByIdAsync(999);
        result.Status.Should().Be(404);
    }
 
    [Fact][Trait("Service","Action")]
    public async Task ActionService_SoftDeleteAsync_NotFound_Returns400()
    {
        var repo = new Mock<IActionRepository>();
        repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Action, bool>>>(), false))
            .ReturnsAsync((Backend.Domain.Entities.Action?)null);
        var result = await new ActionService(repo.Object).SoftDeleteAsync(999);
        result.Status.Should().Be(400);
    }
 
    [Fact][Trait("Service","Action")]
    public async Task ActionService_Create_DuplicateName_ReturnsUnprocessableEntity()
    {
        var repo = new Mock<IActionRepository>();
        repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Action, bool>>>()))
            .ReturnsAsync(true);
        var result = await new ActionService(repo.Object).CreateAsync(new CreateActionDto { Name = "CREATE" });
        result.Status.Should().Be(422);
    }
 
    // ════════════════════════════════════════════════════════════════════════
    // NotificationCategoryService — ctor: (INotificationCategoryRepository)
    // ════════════════════════════════════════════════════════════════════════
    [Fact][Trait("Service","NotificationCategory")]
    public async Task NotificationCategoryService_GetAllAsync_ReturnsSuccess()
    {
        var repo = new Mock<INotificationCategoryRepository>();
        repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Backend.Domain.Entities.NotificationCategory, bool>>>(), It.IsAny<bool>()))
            .Returns(Enumerable.Empty<Backend.Domain.Entities.NotificationCategory>().AsQueryable().BuildMock());
        var result = await new NotificationCategoryService(repo.Object).GetAllAsync();
        result.Should().NotBeNull();
    }
 
    [Fact][Trait("Service","NotificationCategory")]
    public async Task NotificationCategoryService_GetByIdAsync_NotFound_Returns404()
    {
        var repo = new Mock<INotificationCategoryRepository>();
        repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Backend.Domain.Entities.NotificationCategory, bool>>>(), It.IsAny<bool>()))
            .Returns(Enumerable.Empty<Backend.Domain.Entities.NotificationCategory>().AsQueryable().BuildMock());
        var result = await new NotificationCategoryService(repo.Object).GetByIdAsync(999);
        result.Status.Should().Be(404);
    }
 
    // ════════════════════════════════════════════════════════════════════════
    // NotificationTypeService — ctor: (INotificationTypeRepository)
    // ════════════════════════════════════════════════════════════════════════
    [Fact][Trait("Service","NotificationType")]
    public async Task NotificationTypeService_GetAllAsync_ReturnsSuccess()
    {
        var repo = new Mock<INotificationTypeRepository>();
        repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Backend.Domain.Entities.NotificationType, bool>>>(), It.IsAny<bool>()))
            .Returns(Enumerable.Empty<Backend.Domain.Entities.NotificationType>().AsQueryable().BuildMock());
        var result = await new NotificationTypeService(repo.Object).GetAllAsync();
        result.Should().NotBeNull();
    }
 
    [Fact][Trait("Service","NotificationType")]
    public async Task NotificationTypeService_Create_DuplicateName_Returns422()
    {
        var repo = new Mock<INotificationTypeRepository>();
        repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.NotificationType, bool>>>()))
            .ReturnsAsync(true);
        var result = await new NotificationTypeService(repo.Object).CreateAsync(
            new CreateNotificationTypeDto { Name = "Alert" });
        result.Status.Should().Be(422);
    }
 
    // ════════════════════════════════════════════════════════════════════════
    // UserStatusService — ctor: (IUserStatusRepository)
    // ════════════════════════════════════════════════════════════════════════
    [Fact][Trait("Service","UserStatus")]
    public async Task UserStatusService_GetAllAsync_ReturnsSuccess()
    {
        var repo = new Mock<IUserStatusRepository>();
        repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Backend.Domain.Entities.UserStatus, bool>>>(), It.IsAny<bool>()))
            .Returns(Enumerable.Empty<Backend.Domain.Entities.UserStatus>().AsQueryable().BuildMock());
        var result = await new UserStatusService(repo.Object).GetAllAsync();
        result.Should().NotBeNull();
    }
 
    [Fact][Trait("Service","UserStatus")]
    public async Task UserStatusService_Create_DuplicateName_Returns422()
    {
        var repo = new Mock<IUserStatusRepository>();
        repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.UserStatus, bool>>>()))
            .ReturnsAsync(true);
        var result = await new UserStatusService(repo.Object).CreateAsync(
            new CreateUserStatusDto { Name = "Active", Color = "#00FF00" });
        result.Status.Should().Be(422);
    }
 
    // ════════════════════════════════════════════════════════════════════════
    // DashboardService — ctor: (ILogger<DashboardService>, IHttpContextAccessor)
    // Lưu ý: GetReportStatisticsAsync() throw NotImplementedException nên không test
    // ════════════════════════════════════════════════════════════════════════
    [Fact][Trait("Service","Dashboard")]
    public void DashboardService_CanBeInstantiated()
    {
        var loggerMock  = new Mock<Microsoft.Extensions.Logging.ILogger<DashboardService>>();
        var httpContext = MockHelper.HttpContextAccessor();
        var svc = new DashboardService(loggerMock.Object, httpContext.Object);
        svc.Should().NotBeNull();
    }
}
