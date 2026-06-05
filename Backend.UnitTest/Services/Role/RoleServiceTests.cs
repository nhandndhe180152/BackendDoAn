using System;
using System.Linq.Expressions;
using Backend.Application.Constants;
using Backend.Application.DTOs.Roles;
using Backend.Application.Implements;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Services;
using Backend.UnitTest.Common;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;

namespace Backend.UnitTest.Services.Role;

// <summary>
/// Unit tests cho RoleService — tập trung vào CreateAsync và validation business rules.
/// </summary>
public class RoleServiceTests
{
    // ── Mocks ────────────────────────────────────────────────────────────────
    private readonly Mock<IRoleRepository> _roleRepo = new();
    private readonly Mock<IPermissionRepository> _permissionRepo = new();
    private readonly Mock<IActionRepository> _actionRepo = new();
    private readonly Mock<IMenuRepository> _menuRepo = new();
    private readonly Mock<IUserRoleRepository> _userRoleRepo = new();
    private readonly Mock<IActionInMenuRepository> _actionInMenuRepo = new();
    private readonly Mock<ICacheService> _cacheService = new();

    private readonly RoleService _sut;

    public RoleServiceTests()
    {
        _sut = new RoleService(
            _roleRepo.Object,
            _permissionRepo.Object,
            _actionRepo.Object,
            _menuRepo.Object,
            _userRoleRepo.Object,
            _actionInMenuRepo.Object,
            MockHelper.HttpContextAccessor().Object,
            _cacheService.Object
        );
    }

    // ════════════════════════════════════════════════════════════════════════
    // CreateAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "Role")]
    [Trait("Method", "Create")]
    public async Task Create_DuplicateName_ReturnsUnprocessableEntity()
    {
        // Arrange
        var dto = new CreateRoleDto { Name = "Admin", CreatedBy = 1 };
        _roleRepo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<global::Backend.Domain.Entities.Role, bool>>>()))
                 .ReturnsAsync(true);

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
        result.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
    }

    [Fact]
    [Trait("Service", "Role")]
    [Trait("Method", "Create")]
    public async Task Create_RepositoryThrowsException_RollsBackAndReturnsInternalError()
    {
        // Arrange
        var dto = new CreateRoleDto { Name = "NewRole", CreatedBy = 1 };
        _roleRepo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<global::Backend.Domain.Entities.Role, bool>>>()))
                 .ReturnsAsync(false);
        _roleRepo.Setup(r => r.BeginTransactionAsync())
                 .Returns(Task.FromResult<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(null!));
        _roleRepo.Setup(r => r.CreateAsync(It.IsAny<global::Backend.Domain.Entities.Role>()))
                 .ThrowsAsync(new Exception("DB timeout"));

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(500);
        _roleRepo.Verify(r => r.RollbackTransactionAsync(), Times.Once);
    }

    // ════════════════════════════════════════════════════════════════════════
    // SoftDeleteAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "Role")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDelete_RoleNotFound_ReturnsNotFound()
    {
        // Arrange
        _roleRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<global::Backend.Domain.Entities.Role, bool>>>(), false))
                 .ReturnsAsync((global::Backend.Domain.Entities.Role?)null);

        // Act
        var result = await _sut.SoftDeleteAsync(id: 9999);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(400);
    }

    // ════════════════════════════════════════════════════════════════════════
    // GetByIdAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "Role")]
    [Trait("Method", "GetById")]
    public async Task GetById_RoleNotFound_ReturnsNotFound()
    {
        // Arrange
        _roleRepo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<global::Backend.Domain.Entities.Role, bool>>>(), It.IsAny<bool>()))
                 .Returns(Enumerable.Empty<global::Backend.Domain.Entities.Role>().AsQueryable().BuildMock());

        // Act
        var result = await _sut.GetByIdAsync(id: 9999);

        // Assert
        result.Status.Should().Be(404);
    }
}
