using System;
using Backend.Application.DTOs.Actions;
using Backend.Application.DTOs.ActivityLogs;
using Backend.Application.DTOs.FolderUploads;
using Backend.Application.DTOs.Menus;
using Backend.Application.DTOs.NotificationCategories;
using Backend.Application.DTOs.Notifications;
using Backend.Application.DTOs.NotificationTypes;
using Backend.Application.DTOs.Roles;
using Backend.Application.DTOs.SystemConfigs;
using Backend.Application.DTOs.UserStatuses;
using Backend.Application.Validators.Actions;
using Backend.Application.Validators.ActivityLogs;
using Backend.Application.Validators.FolderUploads;
using Backend.Application.Validators.Menus;
using Backend.Application.Validators.NotificationCategories;
using Backend.Application.Validators.Notifications;
using Backend.Application.Validators.NotificationTypes;
using Backend.Application.Validators.Roles;
using Backend.Application.Validators.SystemConfigs;
using Backend.Application.Validators.UserStatuses;
using FluentAssertions;

namespace Backend.UnitTest.Validators;

// ════════════════════════════════════════════════════════════════════════════
// ActionValidator
// ════════════════════════════════════════════════════════════════════════════
public class ActionValidatorTests
{
    private readonly CreateActionDtoValidator _c = new();
    private readonly UpdateActionDtoValidator _u = new();

    [Fact] public void Create_Valid_Passes() => _c.Validate(new CreateActionDto { Name = "CREATE" }).IsValid.Should().BeTrue();
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingName_Fails(string? v) => _c.Validate(new CreateActionDto { Name = v! }).IsValid.Should().BeFalse();
    [Fact] public void Create_NameTooLong_Fails() => _c.Validate(new CreateActionDto { Name = new string('a', 256) }).IsValid.Should().BeFalse();
    [Fact] public void Create_DescriptionTooLong_Fails() => _c.Validate(new CreateActionDto { Name = "OK", Description = new string('a', 501) }).IsValid.Should().BeFalse();
    [Fact] public void Update_Valid_Passes() => _u.Validate(new UpdateActionDto { Id = 1, Name = "UPDATE" }).IsValid.Should().BeTrue();
    [Theory][InlineData(null)][InlineData("")] public void Update_MissingName_Fails(string? v) => _u.Validate(new UpdateActionDto { Id = 1, Name = v! }).IsValid.Should().BeFalse();
}

// ════════════════════════════════════════════════════════════════════════════
// ActivityLogValidator
// ════════════════════════════════════════════════════════════════════════════
public class ActivityLogValidatorTests
{
    private readonly CreateActivityLogDtoValidator _c = new();

    [Fact] public void Create_Valid_Passes() => _c.Validate(new CreateActivityLogDto { Action = "Login" }).IsValid.Should().BeTrue();
    [Fact] public void Create_NoAction_Passes() => _c.Validate(new CreateActivityLogDto { Action = "" }).IsValid.Should().BeTrue();
    [Fact] public void Create_ActionTooLong_Fails() => _c.Validate(new CreateActivityLogDto { Action = new string('a', 256) }).IsValid.Should().BeFalse();
}

// ════════════════════════════════════════════════════════════════════════════
// FolderUploadValidator
// ════════════════════════════════════════════════════════════════════════════
public class FolderUploadValidatorTests
{
    private readonly CreateFolderUploadDtoValidator _c = new();

    [Fact] public void Create_Valid_Passes() => _c.Validate(new CreateFolderUploadDto { FolderName = "My Folder" }).IsValid.Should().BeTrue();
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingName_Fails(string? v) => _c.Validate(new CreateFolderUploadDto { FolderName = v! }).IsValid.Should().BeFalse();
    [Fact] public void Create_NameTooLong_Fails() => _c.Validate(new CreateFolderUploadDto { FolderName = new string('a', 256) }).IsValid.Should().BeFalse();
}

// ════════════════════════════════════════════════════════════════════════════
// MenuValidator
// ════════════════════════════════════════════════════════════════════════════
public class MenuValidatorTests
{
    private readonly CreateMenuDtoValidator _c = new();
    private readonly UpdateMenuDtoValidator _u = new();

    private static CreateMenuDto ValidCreate() => new() { Name = "Dashboard", MenuType = "SIDEBAR" };

    [Fact] public void Create_Valid_Passes() => _c.Validate(ValidCreate()).IsValid.Should().BeTrue();
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingName_Fails(string? v) { var d = ValidCreate(); d.Name = v!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_NameTooLong_Fails() { var d = ValidCreate(); d.Name = new string('a', 256); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingMenuType_Fails(string? v) { var d = ValidCreate(); d.MenuType = v!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_MenuTypeTooLong_Fails() { var d = ValidCreate(); d.MenuType = new string('a', 256); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Update_Valid_Passes() => _u.Validate(new UpdateMenuDto { Id = 1, Name = "Updated Menu", MenuType = "SIDEBAR" }).IsValid.Should().BeTrue();
}

// ════════════════════════════════════════════════════════════════════════════
// NotificationCategoryValidator
// ════════════════════════════════════════════════════════════════════════════
public class NotificationCategoryValidatorTests
{
    private readonly CreateNotificationCategoryDtoValidator _c = new();
    private readonly UpdateNotificationCategoryValidator _u = new();

    private static CreateNotificationCategoryDto ValidCreate() => new() { Name = "System", Color = "#0000FF" };

    [Fact] public void Create_Valid_Passes() => _c.Validate(ValidCreate()).IsValid.Should().BeTrue();
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingName_Fails(string? v) { var d = ValidCreate(); d.Name = v!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_NameTooLong_Fails() { var d = ValidCreate(); d.Name = new string('a', 256); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingColor_Fails(string? v) { var d = ValidCreate(); d.Color = v!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_ColorTooLong_Fails() { var d = ValidCreate(); d.Color = new string('a', 51); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_DescriptionTooLong_Fails() { var d = ValidCreate(); d.Description = new string('a', 501); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Update_Valid_Passes() => _u.Validate(new UpdateNotificationCategoryDto { Id = 1, Name = "Updated", Color = "#FF0000" }).IsValid.Should().BeTrue();
}

// ════════════════════════════════════════════════════════════════════════════
// NotificationTypeValidator
// ════════════════════════════════════════════════════════════════════════════
public class NotificationTypeValidatorTests
{
    private readonly CreateNotificationTypeDtoValidator _c = new();
    private readonly UpdateNotificationTypeDtoValidator _u = new();

    [Fact] public void Create_Valid_Passes() => _c.Validate(new CreateNotificationTypeDto { Name = "Alert" }).IsValid.Should().BeTrue();
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingName_Fails(string? v) => _c.Validate(new CreateNotificationTypeDto { Name = v! }).IsValid.Should().BeFalse();
    [Fact] public void Create_NameTooLong_Fails() => _c.Validate(new CreateNotificationTypeDto { Name = new string('a', 256) }).IsValid.Should().BeFalse();
    [Fact] public void Create_DescriptionTooLong_Fails() => _c.Validate(new CreateNotificationTypeDto { Name = "OK", Description = new string('a', 501) }).IsValid.Should().BeFalse();
    [Fact] public void Update_Valid_Passes() => _u.Validate(new UpdateNotificationTypeDto { Id = 1, Name = "Updated" }).IsValid.Should().BeTrue();
}

// ════════════════════════════════════════════════════════════════════════════
// NotificationValidator
// ════════════════════════════════════════════════════════════════════════════
public class NotificationValidatorTests
{
    private readonly CreateNotificationDtoValidator _c = new();
    private readonly UpdateNotificationDtoValidator _u = new();

    private static CreateNotificationDto ValidCreate() => new() { Title = "New update", Content = "Details here", NotificationCategoryId = 1 };

    [Fact] public void Create_Valid_Passes() => _c.Validate(ValidCreate()).IsValid.Should().BeTrue();
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingTitle_Fails(string? v) { var d = ValidCreate(); d.Title = v!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_TitleTooLong_Fails() { var d = ValidCreate(); d.Title = new string('a', 256); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingContent_Fails(string? v) { var d = ValidCreate(); d.Content = v!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_ContentTooLong_Fails() { var d = ValidCreate(); d.Content = new string('a', 501); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_MissingCategoryId_Fails() { var d = ValidCreate(); d.NotificationCategoryId = 0; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_DirectionIdTooLong_Fails() { var d = ValidCreate(); d.DirectionId = new string('a', 256); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Update_Valid_Passes() => _u.Validate(new UpdateNotificationDto { Id = 1, Title = "Updated", Content = "Content", NotificationCategoryId = 1 }).IsValid.Should().BeTrue();
}

// ════════════════════════════════════════════════════════════════════════════
// RoleValidator
// ════════════════════════════════════════════════════════════════════════════
public class RoleValidatorTests
{
    private readonly CreateRoleDtoValidator _c = new();
    private readonly UpdateRoleDtoValidator _u = new();

    [Fact] public void Create_Valid_Passes() => _c.Validate(new CreateRoleDto { Name = "Admin" }).IsValid.Should().BeTrue();
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingName_Fails(string? v) => _c.Validate(new CreateRoleDto { Name = v! }).IsValid.Should().BeFalse();
    [Fact] public void Create_NameTooLong_Fails() => _c.Validate(new CreateRoleDto { Name = new string('a', 256) }).IsValid.Should().BeFalse();
    [Fact] public void Create_DescriptionTooLong_Fails() => _c.Validate(new CreateRoleDto { Name = "OK", Description = new string('a', 501) }).IsValid.Should().BeFalse();
    [Fact] public void Update_Valid_Passes() => _u.Validate(new UpdateRoleDto { Id = 1, Name = "Updated Role" }).IsValid.Should().BeTrue();
    [Theory][InlineData(null)][InlineData("")] public void Update_MissingName_Fails(string? v) => _u.Validate(new UpdateRoleDto { Id = 1, Name = v! }).IsValid.Should().BeFalse();
}

// ════════════════════════════════════════════════════════════════════════════
// SystemConfigValidator
// ════════════════════════════════════════════════════════════════════════════
public class SystemConfigValidatorTests
{
    private readonly CreateSystemConfigDtoValidator _c = new();
    private readonly UpdateSystemConfigDtoValidator _u = new();

    private static CreateSystemConfigDto ValidCreate() => new() { Name = "Config1", ConfigKey = "KEY_1", ConfigValue = "value1" };

    [Fact] public void Create_Valid_Passes() => _c.Validate(ValidCreate()).IsValid.Should().BeTrue();
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingName_Fails(string? v) { var d = ValidCreate(); d.Name = v!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_NameTooLong_Fails() { var d = ValidCreate(); d.Name = new string('a', 256); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingConfigKey_Fails(string? v) { var d = ValidCreate(); d.ConfigKey = v!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_ConfigKeyTooLong_Fails() { var d = ValidCreate(); d.ConfigKey = new string('a', 256); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingConfigValue_Fails(string? v) { var d = ValidCreate(); d.ConfigValue = v!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_DescriptionTooLong_Fails() { var d = ValidCreate(); d.Description = new string('a', 501); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Update_Valid_Passes() => _u.Validate(new UpdateSystemConfigDto { Id = 1, Name = "Updated", ConfigKey = "K", ConfigValue = "V" }).IsValid.Should().BeTrue();
}

// ════════════════════════════════════════════════════════════════════════════
// UserStatusValidator
// ════════════════════════════════════════════════════════════════════════════
public class UserStatusValidatorTests
{
    private readonly CreateUserStatusDtoValidator _c = new();
    private readonly UpdateUserStatusDtoValidator _u = new();

    private static CreateUserStatusDto ValidCreate() => new() { Name = "Active", Color = "#00FF00" };

    [Fact] public void Create_Valid_Passes() => _c.Validate(ValidCreate()).IsValid.Should().BeTrue();
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingName_Fails(string? v) { var d = ValidCreate(); d.Name = v!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_NameTooLong_Fails() { var d = ValidCreate(); d.Name = new string('a', 256); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingColor_Fails(string? v) { var d = ValidCreate(); d.Color = v!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_ColorTooLong_Fails() { var d = ValidCreate(); d.Color = new string('a', 51); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_DescriptionTooLong_Fails() { var d = ValidCreate(); d.Description = new string('a', 501); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Update_Valid_Passes() => _u.Validate(new UpdateUserStatusDto { Id = 1, Name = "Locked", Color = "#FF0000" }).IsValid.Should().BeTrue();
}
