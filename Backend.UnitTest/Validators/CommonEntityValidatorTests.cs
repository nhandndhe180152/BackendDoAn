using System;
using Backend.Application.DTOs.Actions;
using Backend.Application.DTOs.ActivityLogs;
using Backend.Application.DTOs.Feedbacks;
using Backend.Application.DTOs.FolderUploads;
using Backend.Application.DTOs.Menus;
using Backend.Application.DTOs.NotificationCategories;
using Backend.Application.DTOs.Notifications;
using Backend.Application.DTOs.NotificationTypes;
using Backend.Application.DTOs.PaymentMethods;
using Backend.Application.DTOs.PaymentStatuses;
using Backend.Application.DTOs.Provinces;
using Backend.Application.DTOs.Roles;
using Backend.Application.DTOs.SystemConfigs;
using Backend.Application.DTOs.Tags;
using Backend.Application.DTOs.TagTypes;
using Backend.Application.DTOs.UserStatuses;
using Backend.Application.DTOs.Wards;
using Backend.Application.Validators.Actions;
using Backend.Application.Validators.ActivityLogs;
using Backend.Application.Validators.Feedbacks;
using Backend.Application.Validators.FolderUploads;
using Backend.Application.Validators.Menus;
using Backend.Application.Validators.NotificationCategories;
using Backend.Application.Validators.Notifications;
using Backend.Application.Validators.NotificationTypes;
using Backend.Application.Validators.PaymentMethods;
using Backend.Application.Validators.PaymentStatuses;
using Backend.Application.Validators.Provinces;
using Backend.Application.Validators.Roles;
using Backend.Application.Validators.SystemConfigs;
using Backend.Application.Validators.Tags;
using Backend.Application.Validators.TagTypes;
using Backend.Application.Validators.UserStatuses;
using Backend.Application.Validators.Wards;
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
// FeedbackValidator
// ════════════════════════════════════════════════════════════════════════════
public class FeedbackValidatorTests
{
    private readonly CreateFeedbackDtoValidator _c = new();
    private readonly UpdateFeedbackDtoValidator _u = new();

    private static CreateFeedbackDto ValidCreate() => new() { Title = "Feedback Title", Content = "Feedback Content" };

    [Fact] public void Create_Valid_Passes() => _c.Validate(ValidCreate()).IsValid.Should().BeTrue();
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingTitle_Fails(string? v) { var d = ValidCreate(); d.Title = v!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_TitleTooLong_Fails() { var d = ValidCreate(); d.Title = new string('a', 501); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingContent_Fails(string? v) { var d = ValidCreate(); d.Content = v!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_ContentTooLong_Fails() { var d = ValidCreate(); d.Content = new string('a', 1001); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Update_Valid_Passes() => _u.Validate(new UpdateFeedbackDto { Id = 1, Title = "Updated", Content = "Content" }).IsValid.Should().BeTrue();
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
// PaymentMethodValidator
// ════════════════════════════════════════════════════════════════════════════
public class PaymentMethodValidatorTests
{
    private readonly CreatePaymentMethodDtoValidator _c = new();
    private readonly UpdatePaymentMethodDtoValidator _u = new();

    [Fact] public void Create_Valid_Passes() => _c.Validate(new CreatePaymentMethodDto { Name = "Cash" }).IsValid.Should().BeTrue();
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingName_Fails(string? v) => _c.Validate(new CreatePaymentMethodDto { Name = v! }).IsValid.Should().BeFalse();
    [Fact] public void Create_NameTooLong_Fails() => _c.Validate(new CreatePaymentMethodDto { Name = new string('a', 256) }).IsValid.Should().BeFalse();
    [Fact] public void Create_DescriptionTooLong_Fails() => _c.Validate(new CreatePaymentMethodDto { Name = "OK", Description = new string('a', 501) }).IsValid.Should().BeFalse();
    [Fact] public void Update_Valid_Passes() => _u.Validate(new UpdatePaymentMethodDto { Id = 1, Name = "Transfer" }).IsValid.Should().BeTrue();
}

// ════════════════════════════════════════════════════════════════════════════
// PaymentStatusValidator
// ════════════════════════════════════════════════════════════════════════════
public class PaymentStatusValidatorTests
{
    private readonly CreatePaymentStatusDtoValidator _c = new();
    private readonly UpdatePaymentStatusDtoValidator _u = new();

    private static CreatePaymentStatusDto ValidCreate() => new() { Name = "Pending", Color = "#FFCC00" };

    [Fact] public void Create_Valid_Passes() => _c.Validate(ValidCreate()).IsValid.Should().BeTrue();
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingName_Fails(string? v) { var d = ValidCreate(); d.Name = v!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_NameTooLong_Fails() { var d = ValidCreate(); d.Name = new string('a', 256); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingColor_Fails(string? v) { var d = ValidCreate(); d.Color = v!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_ColorTooLong_Fails() { var d = ValidCreate(); d.Color = new string('a', 51); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_DescriptionTooLong_Fails() { var d = ValidCreate(); d.Description = new string('a', 501); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Update_Valid_Passes() => _u.Validate(new UpdatePaymentStatusDto { Id = 1, Name = "Paid", Color = "#00FF00" }).IsValid.Should().BeTrue();
}

// ════════════════════════════════════════════════════════════════════════════
// ProvinceValidator
// ════════════════════════════════════════════════════════════════════════════
public class ProvinceValidatorTests
{
    private readonly CreateProvinceDtoValidator _c = new();
    private readonly UpdateProvinceDtoValidator _u = new();

    [Fact] public void Create_Valid_Passes() => _c.Validate(new CreateProvinceDto { Name = "Hà Nội" }).IsValid.Should().BeTrue();
    [Fact] public void Create_MissingName_Fails() => _c.Validate(new CreateProvinceDto { Name = null! }).IsValid.Should().BeFalse();
    [Fact] public void Create_NameTooLong_Fails() => _c.Validate(new CreateProvinceDto { Name = new string('a', 256) }).IsValid.Should().BeFalse();
    [Fact] public void Create_CodeTooLong_Fails() => _c.Validate(new CreateProvinceDto { Name = "OK", Code = new string('a', 256) }).IsValid.Should().BeFalse();
    [Fact] public void Create_SlugTooLong_Fails() => _c.Validate(new CreateProvinceDto { Name = "OK", Slug = new string('a', 256) }).IsValid.Should().BeFalse();
    [Fact] public void Create_TypeTooLong_Fails() => _c.Validate(new CreateProvinceDto { Name = "OK", Type = new string('a', 256) }).IsValid.Should().BeFalse();
    [Fact] public void Update_Valid_Passes() => _u.Validate(new UpdateProvinceDto { Id = 1, Name = "Updated" }).IsValid.Should().BeTrue();
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
// TagTypeValidator
// ════════════════════════════════════════════════════════════════════════════
public class TagTypeValidatorTests
{
    private readonly CreateTagTypeDtoValidator _c = new();
    private readonly UpdateTagTypeDtoValidator _u = new();

    [Fact] public void Create_Valid_Passes() => _c.Validate(new CreateTagTypeDto { Name = "Genre" }).IsValid.Should().BeTrue();
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingName_Fails(string? v) => _c.Validate(new CreateTagTypeDto { Name = v! }).IsValid.Should().BeFalse();
    [Fact] public void Create_NameTooLong_Fails() => _c.Validate(new CreateTagTypeDto { Name = new string('a', 256) }).IsValid.Should().BeFalse();
    [Fact] public void Create_DescriptionTooLong_Fails() => _c.Validate(new CreateTagTypeDto { Name = "OK", Description = new string('a', 501) }).IsValid.Should().BeFalse();
    [Fact] public void Update_Valid_Passes() => _u.Validate(new UpdateTagTypeDto { Id = 1, Name = "Updated" }).IsValid.Should().BeTrue();
}

// ════════════════════════════════════════════════════════════════════════════
// TagValidator
// ════════════════════════════════════════════════════════════════════════════
public class TagValidatorTests
{
    private readonly CreateTagDtoValidator _c = new();
    private readonly UpdateTagDtoValidator _u = new();

    private static CreateTagDto ValidCreate() => new() { Name = "AI", TagTypeId = 1 };

    [Fact] public void Create_Valid_Passes() => _c.Validate(ValidCreate()).IsValid.Should().BeTrue();
    [Theory][InlineData(null)][InlineData("")] public void Create_MissingName_Fails(string? v) { var d = ValidCreate(); d.Name = v!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_NameTooLong_Fails() { var d = ValidCreate(); d.Name = new string('a', 256); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_MissingTagTypeId_Fails() { var d = ValidCreate(); d.TagTypeId = 0; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_DescriptionTooLong_Fails() { var d = ValidCreate(); d.Description = new string('a', 501); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Update_Valid_Passes() => _u.Validate(new UpdateTagDto { Id = 1, Name = "ML", TagTypeId = 1 }).IsValid.Should().BeTrue();
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

// ════════════════════════════════════════════════════════════════════════════
// WardValidator
// ════════════════════════════════════════════════════════════════════════════
public class WardValidatorTests
{
    private readonly CreateWardDtoValidator _c = new();
    private readonly UpdateWardDtoValidator _u = new();

    private static CreateWardDto ValidCreate() => new() { Name = "Phường 1", ProvinceId = 1 };

    [Fact] public void Create_Valid_Passes() => _c.Validate(ValidCreate()).IsValid.Should().BeTrue();
    [Fact] public void Create_MissingName_Fails() { var d = ValidCreate(); d.Name = null!; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_NameTooLong_Fails() { var d = ValidCreate(); d.Name = new string('a', 256); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_MissingProvinceId_Fails() { var d = ValidCreate(); d.ProvinceId = null; _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_CodeTooLong_Fails() { var d = ValidCreate(); d.Code = new string('a', 256); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_SlugTooLong_Fails() { var d = ValidCreate(); d.Slug = new string('a', 256); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Create_TypeTooLong_Fails() { var d = ValidCreate(); d.Type = new string('a', 256); _c.Validate(d).IsValid.Should().BeFalse(); }
    [Fact] public void Update_Valid_Passes() => _u.Validate(new UpdateWardDto { Id = 1, Name = "Phường 2", ProvinceId = 1 }).IsValid.Should().BeTrue();
}
