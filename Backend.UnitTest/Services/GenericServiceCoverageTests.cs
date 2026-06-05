using System;
using System.Linq.Expressions;
using Backend.Application.DTOs.Actions;
using Backend.Application.DTOs.BlogPostCategories;
using Backend.Application.DTOs.BlogPostLayouts;
using Backend.Application.DTOs.NotificationTypes;
using Backend.Application.DTOs.PaymentMethods;
using Backend.Application.DTOs.PaymentStatuses;
using Backend.Application.DTOs.Tags;
using Backend.Application.DTOs.TagTypes;
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
    // BlogCategoryService — ctor: (IBlogCategoryRepository)
    // ════════════════════════════════════════════════════════════════════════
    [Fact][Trait("Service","BlogCategory")]
    public async Task BlogCategoryService_GetAllAsync_ReturnsSuccess()
    {
        var repo = new Mock<IBlogCategoryRepository>();
        repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Backend.Domain.Entities.BlogPostCategory, bool>>>(), It.IsAny<bool>()))
            .Returns(Enumerable.Empty<Backend.Domain.Entities.BlogPostCategory>().AsQueryable().BuildMock());
        var result = await new BlogCategoryService(repo.Object).GetAllAsync();
        result.Should().NotBeNull();
    }
 
    [Fact][Trait("Service","BlogCategory")]
    public async Task BlogCategoryService_GetByIdAsync_NotFound_Returns404()
    {
        var repo = new Mock<IBlogCategoryRepository>();
        repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Backend.Domain.Entities.BlogPostCategory, bool>>>(), It.IsAny<bool>()))
            .Returns(Enumerable.Empty<Backend.Domain.Entities.BlogPostCategory>().AsQueryable().BuildMock());
        var result = await new BlogCategoryService(repo.Object).GetByIdAsync(999);
        result.Status.Should().Be(404);
    }
 
    [Fact][Trait("Service","BlogCategory")]
    public async Task BlogCategoryService_Create_DuplicateName_Returns422()
    {
        var repo = new Mock<IBlogCategoryRepository>();
        repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.BlogPostCategory, bool>>>()))
            .ReturnsAsync(true);
        var result = await new BlogCategoryService(repo.Object).CreateAsync(
            new CreateBlogPostCategoryDto { Name = "Tech", Color = "#FF0000" });
        result.Status.Should().Be(422);
    }
 
    // ════════════════════════════════════════════════════════════════════════
    // BlogLayoutService — ctor: (IBlogLayoutRepository)
    // ════════════════════════════════════════════════════════════════════════
    [Fact][Trait("Service","BlogLayout")]
    public async Task BlogLayoutService_GetAllAsync_ReturnsSuccess()
    {
        var repo = new Mock<IBlogLayoutRepository>();
        repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Backend.Domain.Entities.BlogPostLayout, bool>>>(), It.IsAny<bool>()))
            .Returns(Enumerable.Empty<Backend.Domain.Entities.BlogPostLayout>().AsQueryable().BuildMock());
        var result = await new BlogLayoutService(repo.Object).GetAllAsync();
        result.Should().NotBeNull();
    }
 
    [Fact][Trait("Service","BlogLayout")]
    public async Task BlogLayoutService_Create_DuplicateName_Returns422()
    {
        var repo = new Mock<IBlogLayoutRepository>();
        repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.BlogPostLayout, bool>>>()))
            .ReturnsAsync(true);
        var result = await new BlogLayoutService(repo.Object).CreateAsync(
            new CreateBlogPostLayoutDto { Name = "Layout 1" });
        result.Status.Should().Be(422);
    }
 
    // ════════════════════════════════════════════════════════════════════════
    // BlogPostStatusService — ctor: (IBlogPostStatusRepository)
    // ════════════════════════════════════════════════════════════════════════
    [Fact][Trait("Service","BlogPostStatus")]
    public async Task BlogPostStatusService_GetAllAsync_ReturnsSuccess()
    {
        var repo = new Mock<IBlogPostStatusRepository>();
        repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Backend.Domain.Entities.BlogPostStatus, bool>>>(), It.IsAny<bool>()))
            .Returns(Enumerable.Empty<Backend.Domain.Entities.BlogPostStatus>().AsQueryable().BuildMock());
        var result = await new BlogPostStatusService(repo.Object).GetAllAsync();
        result.Should().NotBeNull();
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
    // PaymentMethodService — ctor: (IPaymentMethodRepository)
    // ════════════════════════════════════════════════════════════════════════
    [Fact][Trait("Service","PaymentMethod")]
    public async Task PaymentMethodService_GetAllAsync_ReturnsSuccess()
    {
        var repo = new Mock<IPaymentMethodRepository>();
        repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Backend.Domain.Entities.PaymentMethod, bool>>>(), It.IsAny<bool>()))
            .Returns(Enumerable.Empty<Backend.Domain.Entities.PaymentMethod>().AsQueryable().BuildMock());
        var result = await new PaymentMethodService(repo.Object).GetAllAsync();
        result.Should().NotBeNull();
    }
 
    [Fact][Trait("Service","PaymentMethod")]
    public async Task PaymentMethodService_Create_DuplicateName_Returns422()
    {
        var repo = new Mock<IPaymentMethodRepository>();
        repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.PaymentMethod, bool>>>()))
            .ReturnsAsync(true);
        var result = await new PaymentMethodService(repo.Object).CreateAsync(
            new CreatePaymentMethodDto { Name = "Cash" });
        result.Status.Should().Be(422);
    }
 
    // ════════════════════════════════════════════════════════════════════════
    // PaymentStatusService — ctor: (IPaymentStatusRepository)
    // ════════════════════════════════════════════════════════════════════════
    [Fact][Trait("Service","PaymentStatus")]
    public async Task PaymentStatusService_GetAllAsync_ReturnsSuccess()
    {
        var repo = new Mock<IPaymentStatusRepository>();
        repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Backend.Domain.Entities.PaymentStatus, bool>>>(), It.IsAny<bool>()))
            .Returns(Enumerable.Empty<Backend.Domain.Entities.PaymentStatus>().AsQueryable().BuildMock());
        var result = await new PaymentStatusService(repo.Object).GetAllAsync();
        result.Should().NotBeNull();
    }
 
    [Fact][Trait("Service","PaymentStatus")]
    public async Task PaymentStatusService_Create_DuplicateName_Returns422()
    {
        var repo = new Mock<IPaymentStatusRepository>();
        repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.PaymentStatus, bool>>>()))
            .ReturnsAsync(true);
        var result = await new PaymentStatusService(repo.Object).CreateAsync(
            new CreatePaymentStatusDto { Name = "Pending", Color = "#FFCC00" });
        result.Status.Should().Be(422);
    }
 
    // ════════════════════════════════════════════════════════════════════════
    // TagTypeService — ctor: (ITagTypeRepository)
    // ════════════════════════════════════════════════════════════════════════
    [Fact][Trait("Service","TagType")]
    public async Task TagTypeService_GetAllAsync_ReturnsSuccess()
    {
        var repo = new Mock<ITagTypeRepository>();
        repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Backend.Domain.Entities.TagType, bool>>>(), It.IsAny<bool>()))
            .Returns(Enumerable.Empty<Backend.Domain.Entities.TagType>().AsQueryable().BuildMock());
        var result = await new TagTypeService(repo.Object).GetAllAsync();
        result.Should().NotBeNull();
    }
 
    [Fact][Trait("Service","TagType")]
    public async Task TagTypeService_GetByIdAsync_NotFound_Returns404()
    {
        var repo = new Mock<ITagTypeRepository>();
        repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Backend.Domain.Entities.TagType, bool>>>(), It.IsAny<bool>()))
            .Returns(Enumerable.Empty<Backend.Domain.Entities.TagType>().AsQueryable().BuildMock());
        var result = await new TagTypeService(repo.Object).GetByIdAsync(999);
        result.Status.Should().Be(404);
    }
 
    [Fact][Trait("Service","TagType")]
    public async Task TagTypeService_SoftDeleteAsync_NotFound_Returns400()
    {
        var repo = new Mock<ITagTypeRepository>();
        repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.TagType, bool>>>(), false))
            .ReturnsAsync((Backend.Domain.Entities.TagType?)null);
        var result = await new TagTypeService(repo.Object).SoftDeleteAsync(999);
        result.Status.Should().Be(400);
    }
 
    [Fact][Trait("Service","TagType")]
    public async Task TagTypeService_Create_DuplicateName_Returns422()
    {
        var repo = new Mock<ITagTypeRepository>();
        repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.TagType, bool>>>()))
            .ReturnsAsync(true);
        var result = await new TagTypeService(repo.Object).CreateAsync(
            new CreateTagTypeDto { Name = "Genre" });
        result.Status.Should().Be(422);
    }
 
    // ════════════════════════════════════════════════════════════════════════
    // TagService — ctor: (ITagRepository)
    // ════════════════════════════════════════════════════════════════════════
    [Fact][Trait("Service","Tag")]
    public async Task TagService_GetAllAsync_ReturnsSuccess()
    {
        var repo = new Mock<ITagRepository>();
        repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Backend.Domain.Entities.Tag, bool>>>(), It.IsAny<bool>()))
            .Returns(Enumerable.Empty<Backend.Domain.Entities.Tag>().AsQueryable().BuildMock());
        var result = await new TagService(repo.Object).GetAllAsync();
        result.Should().NotBeNull();
    }
 
    [Fact][Trait("Service","Tag")]
    public async Task TagService_GetByIdAsync_NotFound_Returns404()
    {
        var repo = new Mock<ITagRepository>();
        repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Backend.Domain.Entities.Tag, bool>>>(), It.IsAny<bool>()))
            .Returns(Enumerable.Empty<Backend.Domain.Entities.Tag>().AsQueryable().BuildMock());
        var result = await new TagService(repo.Object).GetByIdAsync(999);
        result.Status.Should().Be(404);
    }
 
    [Fact][Trait("Service","Tag")]
    public async Task TagService_Create_DuplicateTag_Returns422()
    {
        var repo = new Mock<ITagRepository>();
        repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Tag, bool>>>()))
            .ReturnsAsync(true);
        var result = await new TagService(repo.Object).CreateAsync(
            new CreateTagDto { Name = "AI", TagTypeId = 1 });
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
