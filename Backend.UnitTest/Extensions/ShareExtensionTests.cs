using System;
using Backend.Share.Helpers;
using Backend.Share.Extensions;
using FluentAssertions;

namespace Backend.UnitTest.Extensions;

// ════════════════════════════════════════════════════════════════════════════
// StringExtensions
// ════════════════════════════════════════════════════════════════════════════
public class StringExtensionsTests
{
//     [Theory]
//     [InlineData("hello world",   "hello world")]
//     [InlineData("  hello  ",     "hello")]
//     [InlineData("hello  world",  "hello world")]
//     [InlineData("",              "")]
//     [InlineData(null,            "")]
//     public void TrimAll_VariousInputs_ReturnsExpected(string? input, string expected)
//         => (input?.TrimAll() ?? string.Empty).Should().Be(expected);
 
//     [Theory]
//     [InlineData("hello",      "Hello")]
//     [InlineData("HELLO",      "Hello")]
//     [InlineData("hello world","Hello world")]
//     [InlineData("",           "")]
//     public void ToTitleCase_VariousInputs_ReturnsExpected(string input, string expected)
//         => input.ToTitleCase().Should().Be(expected);
 
//     [Theory]
//     [InlineData("hello",  true)]
//     [InlineData("",       false)]
//     [InlineData(null,     false)]
//     [InlineData("  ",     false)]
//     public void HasValue_VariousInputs_ReturnsExpected(string? input, bool expected)
//         => input.HasValue().Should().Be(expected);
 
//     [Theory]
//     [InlineData(null, true)]
//     [InlineData("",   true)]
//     [InlineData("x",  false)]
//     public void IsNullOrEmpty_VariousInputs_ReturnsExpected(string? input, bool expected)
//         => input.IsNullOrEmpty().Should().Be(expected);
 
//     [Theory]
//     [InlineData("ABC123", "abc123")]
//     [InlineData("Hello",  "hello")]
//     [InlineData("",       "")]
//     public void ToLowerTrim_VariousInputs_ReturnsExpected(string input, string expected)
//         => input.ToLowerTrim().Should().Be(expected);
 
//     [Theory]
//     [InlineData("abc", "ABC")]
//     [InlineData("",    "")]
//     public void ToUpperTrim_VariousInputs_ReturnsExpected(string input, string expected)
//         => input.ToUpperTrim().Should().Be(expected);
 
//     [Theory]
//     [InlineData("12345",  true)]
//     [InlineData("123.45", false)]
//     [InlineData("abc",    false)]
//     [InlineData("",       false)]
//     [InlineData(null,     false)]
//     public void IsNumeric_VariousInputs_ReturnsExpected(string? input, bool expected)
//         => input.IsNumeric().Should().Be(expected);
 
//     [Fact]
//     public void MaskEmail_ValidEmail_MasksCorrectly()
//     {
//         var result = "user@example.com".MaskEmail();
//         result.Should().Contain("@");
//         result.Should().Contain("*");
//     }
 
//     [Fact]
//     public void MaskPhone_ValidPhone_MasksCorrectly()
//     {
//         var result = "0912345678".MaskPhone();
//         result.Should().Contain("*");
//         result.Length.Should().Be("0912345678".Length);
//     }
// }
 
// // ════════════════════════════════════════════════════════════════════════════
// // LinqExtensions
// // ════════════════════════════════════════════════════════════════════════════
// public class LinqExtensionsTests
// {
//     [Fact]
//     public void WhereIf_ConditionTrue_AppliesFilter()
//     {
//         var list   = new[] { 1, 2, 3, 4, 5 }.AsQueryable();
//         var result = list.WhereIf(true, x => x > 3).ToList();
//         result.Should().BeEquivalentTo(new[] { 4, 5 });
//     }
 
//     [Fact]
//     public void WhereIf_ConditionFalse_ReturnsAll()
//     {
//         var list   = new[] { 1, 2, 3, 4, 5 }.AsQueryable();
//         var result = list.WhereIf(false, x => x > 3).ToList();
//         result.Should().HaveCount(5);
//     }
 
//     [Fact]
//     public void OrderByIf_ConditionTrue_SortsDescending()
//     {
//         var list   = new[] { 3, 1, 2 }.AsQueryable();
//         var result = list.OrderByDescendingIf(true, x => x).ToList();
//         result.Should().BeInDescendingOrder();
//     }
 
//     [Fact]
//     public void OrderByIf_ConditionFalse_SortsAscending()
//     {
//         var list   = new[] { 3, 1, 2 }.AsQueryable();
//         var result = list.OrderByIf(false, x => x).ToList();
//         // condition false = no ordering applied
//         result.Should().HaveCount(3);
//     }
// }
 
// // ════════════════════════════════════════════════════════════════════════════
// // RandomHelper — additional tests beyond what already exists
// // ════════════════════════════════════════════════════════════════════════════
// public class RandomHelperTests
// {
//     [Fact]
//     public void GenerateOtpCode_Returns6Digits()
//     {
//         var otp = RandomHelper.GenerateOtpCode();
//         otp.Should().HaveLength(6);
//         otp.Should().MatchRegex(@"^\d{6}$");
//     }
 
//     [Theory]
//     [InlineData(10)]
//     [InlineData(20)]
//     [InlineData(50)]
//     public void GenerateRandomString_ReturnsCorrectLength(int length)
//     {
//         var result = RandomHelper.GenerateRandomString(length);
//         result.Should().HaveLength(length);
//     }
 
//     [Fact]
//     public void GenerateRandomString_TwoCalls_ProduceDifferentResults()
//     {
//         var r1 = RandomHelper.GenerateRandomString(30);
//         var r2 = RandomHelper.GenerateRandomString(30);
//         r1.Should().NotBe(r2); // Extremely unlikely to be equal
//     }
// }
 
// // ════════════════════════════════════════════════════════════════════════════
// // FileHelper
// // ════════════════════════════════════════════════════════════════════════════
// public class FileHelperTests
// {
//     [Theory]
//     [InlineData("photo.jpg",  ".jpg")]
//     [InlineData("doc.PDF",    ".PDF")]
//     [InlineData("noextension","")]
//     [InlineData("",           "")]
//     public void GetExtension_VariousFiles_ReturnsExpected(string filename, string expected)
//         => FileHelper.GetExtension(filename).Should().Be(expected);
 
//     [Theory]
//     [InlineData("image.jpg",  true)]
//     [InlineData("image.jpeg", true)]
//     [InlineData("image.png",  true)]
//     [InlineData("image.gif",  true)]
//     [InlineData("image.webp", true)]
//     [InlineData("doc.pdf",    false)]
//     [InlineData("data.xlsx",  false)]
//     [InlineData("",           false)]
//     public void IsImage_VariousFiles_ReturnsExpected(string filename, bool expected)
//         => FileHelper.IsImage(filename).Should().Be(expected);
 
//     [Theory]
//     [InlineData(0,           "0 B")]
//     [InlineData(1024,        "1 KB")]
//     [InlineData(1048576,     "1 MB")]
//     [InlineData(1073741824L, "1 GB")]
//     public void FormatSize_VariousSizes_ReturnsReadableString(long bytes, string expected)
//         => FileHelper.FormatSize(bytes).Should().Be(expected);
 
//     [Theory]
//     [InlineData("report.pdf",   true)]
//     [InlineData("doc.docx",     true)]
//     [InlineData("data.xlsx",    true)]
//     [InlineData("image.jpg",    false)]
//     [InlineData("",             false)]
//     public void IsDocument_VariousFiles_ReturnsExpected(string filename, bool expected)
//         => FileHelper.IsDocument(filename).Should().Be(expected);
// }
 
// // ════════════════════════════════════════════════════════════════════════════
// // ApiResponse — factory methods coverage
// // ════════════════════════════════════════════════════════════════════════════
// public class ApiResponseTests
// {
//     [Fact] public void Success_WithData_ReturnsCorrectShape()
//     {
//         var r = Backend.Share.Entities.ApiResponse.Success(data: "hello");
//         r.IsSucceeded.Should().BeTrue();
//         r.Status.Should().Be(200);
//         r.Resources.Should().Be("hello");
//     }
 
//     [Fact] public void Success_NoData_ReturnsCorrectShape()
//     {
//         var r = Backend.Share.Entities.ApiResponse.Success();
//         r.IsSucceeded.Should().BeTrue();
//         r.Status.Should().Be(200);
//         r.Resources.Should().BeNull();
//     }
 
//     [Fact] public void Created_WithData_Returns201()
//     {
//         var r = Backend.Share.Entities.ApiResponse.Created(data: 42);
//         r.IsSucceeded.Should().BeTrue();
//         r.Status.Should().Be(201);
//         r.Resources.Should().Be(42);
//     }
 
//     [Fact] public void Created_NoData_Returns201()
//     {
//         var r = Backend.Share.Entities.ApiResponse.Created();
//         r.Status.Should().Be(201);
//     }
 
//     [Fact] public void NotFound_NoData_Returns404()
//     {
//         var r = Backend.Share.Entities.ApiResponse.NotFound();
//         r.IsSucceeded.Should().BeFalse();
//         r.Status.Should().Be(404);
//     }
 
//     [Fact] public void NotFound_WithData_Returns404()
//     {
//         var r = Backend.Share.Entities.ApiResponse.NotFound(errors: "detail", message: "Not found");
//         r.IsSucceeded.Should().BeFalse();
//         r.Status.Should().Be(404);
//     }
 
//     [Fact] public void Unauthorized_NoData_Returns401()
//     {
//         var r = Backend.Share.Entities.ApiResponse.Unauthorized();
//         r.Status.Should().Be(401);
//         r.IsSucceeded.Should().BeFalse();
//     }
 
//     [Fact] public void Unauthorized_WithData_Returns401()
//     {
//         var r = Backend.Share.Entities.ApiResponse.Unauthorized(errors: new { }, message: "Unauthorized");
//         r.Status.Should().Be(401);
//     }
 
//     [Fact] public void Forbidden_NoData_Returns403()
//     {
//         var r = Backend.Share.Entities.ApiResponse.Forbidden();
//         r.Status.Should().Be(403);
//         r.IsSucceeded.Should().BeFalse();
//     }
 
//     [Fact] public void Forbidden_WithData_Returns403()
//     {
//         var r = Backend.Share.Entities.ApiResponse.Forbidden(errors: "detail");
//         r.Status.Should().Be(403);
//     }
 
//     [Fact] public void BadRequest_NoData_Returns400()
//     {
//         var r = Backend.Share.Entities.ApiResponse.BadRequest();
//         r.IsSucceeded.Should().BeFalse();
//         r.Status.Should().Be(400);
//     }
 
//     [Fact] public void BadRequest_WithData_Returns400()
//     {
//         var r = Backend.Share.Entities.ApiResponse.BadRequest(errors: new[] { "error1" }, message: "Validation");
//         r.Status.Should().Be(400);
//         r.Errors.Should().NotBeNull();
//     }
 
//     [Fact] public void UnprocessableEntity_WithErrors_Returns422()
//     {
//         var errors = new List<Backend.Share.Entities.FormErrorMessage>
//         {
//             new() { Field = "Name", Message = new List<string> { "Required" } }
//         };
//         var r = Backend.Share.Entities.ApiResponse.UnprocessableEntity(errors, "Validation failed");
//         r.Status.Should().Be(422);
//         r.IsSucceeded.Should().BeFalse();
//     }
 
//     [Fact] public void UnprocessableEntity_NoErrors_Returns422()
//     {
//         var r = Backend.Share.Entities.ApiResponse.UnprocessableEntity("Invalid data");
//         r.Status.Should().Be(422);
//     }
 
//     [Fact] public void InternalServerError_Returns500()
//     {
//         var r = Backend.Share.Entities.ApiResponse.InternalServerError();
//         r.IsSucceeded.Should().BeFalse();
//         r.Status.Should().Be(500);
//     }
 
//     [Fact] public void Error_WithMessage_ReturnsFailedResponse()
//     {
//         var r = Backend.Share.Entities.ApiResponse.Error();
//         r.IsSucceeded.Should().BeFalse();
//         r.Status.Should().Be(400);
//     }
 
//     [Fact] public void Error_WithErrors_ReturnsFailedResponse()
//     {
//         var r = Backend.Share.Entities.ApiResponse.Error(errors: new[] { "e1" }, message: "Error", status: 400);
//         r.IsSucceeded.Should().BeFalse();
//         r.Errors.Should().NotBeNull();
//     }
}
