using System;
using Backend.Application.DTOs.BlogPostCategories;
using Backend.Application.DTOs.BlogPostLayouts;
using Backend.Application.DTOs.BlogPosts;
using Backend.Application.Validators.BlogCategories;
using Backend.Application.Validators.BlogLayouts;
using Backend.Application.Validators.BlogPosts;
using FluentAssertions;

namespace Backend.UnitTest.Validators.BlogPosts;

public class BlogPostValidatorTests
{
    private readonly CreateBlogPostDtoValidator _createValidator = new();
    private readonly UpdateBlogPostDtoValidator _updateValidator = new();

    private static CreateBlogPostDto ValidCreate() => new()
    {
        Title = "Bài viết test",
        Content = "Nội dung bài viết dài đầy đủ",
        Description = "Mô tả ngắn",
        BlogPostCategoryId = 1,
        BlogPostLayoutId = 1,
        BlogPostStatusId = 1
    };

    [Fact]
    public void CreateBlogPost_Valid_Passes()
        => _createValidator.Validate(ValidCreate()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateBlogPost_MissingTitle_Fails(string? val)
    { var dto = ValidCreate(); dto.Title = val!; _createValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Fact]
    public void CreateBlogPost_TitleTooLong_Fails()
    { var dto = ValidCreate(); dto.Title = new string('A', 501); _createValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateBlogPost_MissingContent_Fails(string? val)
    { var dto = ValidCreate(); dto.Content = val!; _createValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateBlogPost_MissingDescription_Fails(string? val)
    { var dto = ValidCreate(); dto.Description = val!; _createValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Fact]
    public void CreateBlogPost_DescriptionTooLong_Fails()
    { var dto = ValidCreate(); dto.Description = new string('A', 501); _createValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Fact]
    public void CreateBlogPost_MissingCategory_Fails()
    { var dto = ValidCreate(); dto.BlogPostCategoryId = 0; _createValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Fact]
    public void CreateBlogPost_MissingLayout_Fails()
    { var dto = ValidCreate(); dto.BlogPostLayoutId = 0; _createValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Fact]
    public void CreateBlogPost_MissingStatus_Fails()
    { var dto = ValidCreate(); dto.BlogPostStatusId = 0; _createValidator.Validate(dto).IsValid.Should().BeFalse(); }

    // UpdateBlogPostDtoValidator
    [Fact]
    public void UpdateBlogPost_Valid_Passes()
        => _updateValidator.Validate(new UpdateBlogPostDto
        {
            Id = 1,
            Title = "Title",
            Content = "Content",
            Description = "Desc",
            BlogPostCategoryId = 1,
            BlogPostLayoutId = 1,
            BlogPostStatusId = 1
        }).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void UpdateBlogPost_MissingTitle_Fails(string? val)
    {
        var dto = new UpdateBlogPostDto { Id = 1, Title = val!, Content = "c", Description = "d", BlogPostCategoryId = 1, BlogPostLayoutId = 1, BlogPostStatusId = 1 };
        _updateValidator.Validate(dto).IsValid.Should().BeFalse();
    }
}

// ════════════════════════════════════════════════════════════════════════════
// BlogCategoryValidator
// ════════════════════════════════════════════════════════════════════════════
public class BlogCategoryValidatorTests
{
    private readonly CreateBlogCategoryDtoValidator _createValidator = new();
    private readonly UpdateBlogCategoryValidator _updateValidator = new();

    private static CreateBlogPostCategoryDto ValidCreate() => new() { Name = "Tech", Color = "#FF0000", Description = null };

    [Fact]
    public void CreateBlogCategory_Valid_Passes()
        => _createValidator.Validate(ValidCreate()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateBlogCategory_MissingName_Fails(string? val)
    { var dto = ValidCreate(); dto.Name = val!; _createValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Fact]
    public void CreateBlogCategory_NameTooLong_Fails()
    { var dto = ValidCreate(); dto.Name = new string('a', 256); _createValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateBlogCategory_MissingColor_Fails(string? val)
    { var dto = ValidCreate(); dto.Color = val!; _createValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Fact]
    public void CreateBlogCategory_ColorTooLong_Fails()
    { var dto = ValidCreate(); dto.Color = new string('a', 51); _createValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Fact]
    public void CreateBlogCategory_DescriptionTooLong_Fails()
    { var dto = ValidCreate(); dto.Description = new string('a', 501); _createValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Fact]
    public void UpdateBlogCategory_Valid_Passes()
        => _updateValidator.Validate(new UpdateBlogPostCategoryDto { Id = 1, Name = "Tech", Color = "#FF5500" }).IsValid.Should().BeTrue();
}

// ════════════════════════════════════════════════════════════════════════════
// BlogLayoutValidator
// ════════════════════════════════════════════════════════════════════════════
public class BlogLayoutValidatorTests
{
    private readonly CreateBlogLayoutDtoValidator _createValidator = new();
    private readonly UpdateBlogLayoutDtoValidator _updateValidator = new();

    [Fact]
    public void CreateBlogLayout_Valid_Passes()
        => _createValidator.Validate(new CreateBlogPostLayoutDto { Name = "Layout 1", Description = null }).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateBlogLayout_MissingName_Fails(string? val)
        => _createValidator.Validate(new CreateBlogPostLayoutDto { Name = val! }).IsValid.Should().BeFalse();

    [Fact]
    public void CreateBlogLayout_NameTooLong_Fails()
        => _createValidator.Validate(new CreateBlogPostLayoutDto { Name = new string('a', 256) }).IsValid.Should().BeFalse();

    [Fact]
    public void CreateBlogLayout_DescriptionTooLong_Fails()
        => _createValidator.Validate(new CreateBlogPostLayoutDto { Name = "OK", Description = new string('a', 501) }).IsValid.Should().BeFalse();

    [Fact]
    public void UpdateBlogLayout_Valid_Passes()
        => _updateValidator.Validate(new UpdateBlogPostLayoutDto { Id = 1, Name = "Layout updated" }).IsValid.Should().BeTrue();
}
