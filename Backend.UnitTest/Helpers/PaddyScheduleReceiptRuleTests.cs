using Backend.Share.Helpers;
using FluentAssertions;
using Xunit;

namespace Backend.UnitTest.Helpers;

/// <summary>
/// Quy tắc chặn lập phiếu mua trùng trên cùng một lịch thu mua.
/// Web, mobile và backend đều dựa vào quy tắc này nên phải khóa chặt bằng test.
/// </summary>
[Trait("Helper", "PaddyScheduleReceiptRule")]
public class PaddyScheduleReceiptRuleTests
{
    [Theory]
    [InlineData("CANCELLED", true)]
    [InlineData("STOCKED", true)]
    [InlineData("PARTIALLY_STOCKED", true)]
    [InlineData("cancelled", true)]
    [InlineData("NEW", false)]
    [InlineData("CONFIRMED", false)]
    [InlineData("WEIGHED", false)]
    [InlineData(null, false)]
    public void IsBlockedByStatus_ChecksScheduleStatus(string? statusCode, bool expected)
    {
        PaddyScheduleReceiptRule.IsBlockedByStatus(statusCode).Should().Be(expected);
    }

    [Fact]
    public void IsFullyReceipted_WhenTotalWeightReachesEstimate_ReturnsTrue()
    {
        PaddyScheduleReceiptRule.IsFullyReceipted(2000m, 2000m, 2).Should().BeTrue();
        PaddyScheduleReceiptRule.IsFullyReceipted(2000m, 2500m, 3).Should().BeTrue();
    }

    [Fact]
    public void IsFullyReceipted_WhenTotalWeightBelowEstimate_ReturnsFalse()
    {
        PaddyScheduleReceiptRule.IsFullyReceipted(2000m, 1999m, 1).Should().BeFalse();
    }

    [Fact]
    public void IsFullyReceipted_ToleratesRoundingWithin1Gram()
    {
        // Cân theo bao làm tròn 0,1 kg nên tổng có thể thiếu vài phần nghìn kg.
        PaddyScheduleReceiptRule.IsFullyReceipted(2000m, 1999.9995m, 1).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void IsFullyReceipted_WithoutEstimate_AllowsOnlyOneReceipt(double? estimate)
    {
        var estimated = estimate.HasValue ? (decimal?)(decimal)estimate.Value : null;

        PaddyScheduleReceiptRule.IsFullyReceipted(estimated, 0m, 0).Should().BeFalse();
        PaddyScheduleReceiptRule.IsFullyReceipted(estimated, 500m, 1).Should().BeTrue();
    }

    [Fact]
    public void CanCreateReceipt_BlocksWhenStatusOrWeightSaysSo()
    {
        // Còn khối lượng nhưng lịch đã hủy → chặn.
        PaddyScheduleReceiptRule.CanCreateReceipt("CANCELLED", 2000m, 0m, 0).Should().BeFalse();
        // Trạng thái hợp lệ và chưa đủ khối lượng → cho lập tiếp.
        PaddyScheduleReceiptRule.CanCreateReceipt("CONFIRMED", 2000m, 800m, 1).Should().BeTrue();
        // Trạng thái hợp lệ nhưng đã đủ khối lượng → chặn (lỗ hổng cũ của FE web).
        PaddyScheduleReceiptRule.CanCreateReceipt("CONFIRMED", 2000m, 2000m, 2).Should().BeFalse();
    }

    [Fact]
    public void RemainingQtyKg_NeverNegative_AndNullWithoutEstimate()
    {
        PaddyScheduleReceiptRule.RemainingQtyKg(2000m, 800m).Should().Be(1200m);
        PaddyScheduleReceiptRule.RemainingQtyKg(2000m, 2500m).Should().Be(0m);
        PaddyScheduleReceiptRule.RemainingQtyKg(null, 800m).Should().BeNull();
        PaddyScheduleReceiptRule.RemainingQtyKg(0m, 0m).Should().BeNull();
    }
}
