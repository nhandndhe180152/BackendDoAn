using Asp.Versioning;
using Backend.Application.DTOs.Dashboard;
using Backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend.API.Utilities;
using Backend.Domain.Enums;

namespace Backend.API.Controllers;

[Authorize]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/reports")]
[ApiController]
public class ReportsController : BaseController
{
    private readonly IDashboardService _dashboardService;

    public ReportsController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>Bộ dữ liệu dropdown dùng chung cho màn Báo cáo & Phân tích.</summary>
    [HttpGet("filter-options")]
    [CustomAuthorize(Enums.Menu.REPORTS, Enums.Action.READ)]
    public async Task<IActionResult> GetFilterOptionsAsync()
    {
        var result = await _dashboardService.GetReportFilterOptionsAsync();
        return BaseResult(result);
    }

    /// <summary>Các KPI tổng quan báo cáo trong kỳ đã chọn.</summary>
    [HttpGet("overview")]
    [CustomAuthorize(Enums.Menu.REPORTS, Enums.Action.READ)]
    public async Task<IActionResult> GetOverviewAsync([FromQuery] DashboardQuery query)
    {
        var result = await _dashboardService.GetReportsOverviewAsync(query);
        return BaseResult(result);
    }

    /// <summary>Báo cáo tồn kho chi tiết theo từng lô hàng.</summary>
    [HttpGet("inventory-by-lot")]
    [CustomAuthorize(Enums.Menu.REPORTS, Enums.Action.READ)]
    public async Task<IActionResult> GetInventoryByLotReportAsync([FromQuery] DashboardQuery query)
    {
        var result = await _dashboardService.GetInventoryByLotReportAsync(query);
        return BaseResult(result);
    }

    /// <summary>Báo cáo tổng hợp tồn kho theo từng kho hàng.</summary>
    [HttpGet("inventory-by-warehouse")]
    [CustomAuthorize(Enums.Menu.REPORTS, Enums.Action.READ)]
    public async Task<IActionResult> GetInventoryByWarehouseReportAsync([FromQuery] DashboardQuery query)
    {
        var result = await _dashboardService.GetInventoryByWarehouseReportAsync(query);
        return BaseResult(result);
    }

    /// <summary>Báo cáo tổng hợp tồn kho theo biến thể sản phẩm.</summary>
    [HttpGet("inventory-by-product-variant")]
    [CustomAuthorize(Enums.Menu.REPORTS, Enums.Action.READ)]
    public async Task<IActionResult> GetInventoryByProductVariantReportAsync([FromQuery] DashboardQuery query)
    {
        var result = await _dashboardService.GetInventoryByProductVariantReportAsync(query);
        return BaseResult(result);
    }

    /// <summary>Báo cáo công nợ hai chiều nông dân (PAYABLE) và khách hàng (RECEIVABLE).</summary>
    [HttpGet("two-way-debt")]
    [CustomAuthorize(Enums.Menu.REPORTS, Enums.Action.READ)]
    public async Task<IActionResult> GetTwoWayDebtReportAsync([FromQuery] DashboardQuery query)
    {
        var result = await _dashboardService.GetTwoWayDebtReportAsync(query);
        return BaseResult(result);
    }

    /// <summary>Công nợ hai chiều chi tiết theo từng chứng từ.</summary>
    [HttpGet("debt-documents")]
    [CustomAuthorize(Enums.Menu.REPORTS, Enums.Action.READ)]
    public async Task<IActionResult> GetDebtDocumentsReportAsync([FromQuery] DashboardQuery query)
    {
        var result = await _dashboardService.GetDebtDocumentsReportAsync(query);
        return BaseResult(result);
    }

    /// <summary>Báo cáo thu mua lúa từ các phiếu mua thực tế.</summary>
    [HttpGet("purchase")]
    [CustomAuthorize(Enums.Menu.REPORTS, Enums.Action.READ)]
    public async Task<IActionResult> GetPurchaseReportAsync([FromQuery] DashboardQuery query)
    {
        var result = await _dashboardService.GetPurchaseReportAsync(query);
        return BaseResult(result);
    }

    /// <summary>Báo cáo hiệu suất xay xát thực tế (Yield Rate) theo đơn/kho.</summary>
    [HttpGet("milling-yield")]
    [CustomAuthorize(Enums.Menu.REPORTS, Enums.Action.READ)]
    public async Task<IActionResult> GetMillingYieldReportAsync([FromQuery] DashboardQuery query)
    {
        var result = await _dashboardService.GetMillingYieldReportAsync(query);
        return BaseResult(result);
    }

    /// <summary>Báo cáo chi tiết doanh thu bán hàng & số tiền thực tế đã thu.</summary>
    [HttpGet("sales-revenue")]
    [CustomAuthorize(Enums.Menu.REPORTS, Enums.Action.READ)]
    public async Task<IActionResult> GetSalesRevenueReportAsync([FromQuery] DashboardQuery query)
    {
        var result = await _dashboardService.GetSalesRevenueReportAsync(query);
        return BaseResult(result);
    }

    /// <summary>Báo cáo cảnh báo chất lượng: các lô bị cách ly hoặc trễ kiểm định.</summary>
    [HttpGet("quality-alerts")]
    [CustomAuthorize(Enums.Menu.REPORTS, Enums.Action.READ)]
    public async Task<IActionResult> GetQualityAlertsReportAsync([FromQuery] DashboardQuery query)
    {
        var result = await _dashboardService.GetQualityAlertsReportAsync(query);
        return BaseResult(result);
    }

    /// <summary>Báo cáo lãi/lỗ tương đối từ các phiếu xuất đã hoàn tất.</summary>
    [HttpGet("relative-profit")]
    [CustomAuthorize(Enums.Menu.REPORTS, Enums.Action.READ)]
    public async Task<IActionResult> GetRelativeProfitReportAsync([FromQuery] DashboardQuery query)
    {
        var result = await _dashboardService.GetRelativeProfitReportAsync(query);
        return BaseResult(result);
    }

    /// <summary>Phân tích hiệu quả nguồn mua theo nông dân.</summary>
    [HttpGet("source-effectiveness")]
    [CustomAuthorize(Enums.Menu.REPORTS, Enums.Action.READ)]
    public async Task<IActionResult> GetSourceEffectivenessReportAsync([FromQuery] DashboardQuery query)
    {
        var result = await _dashboardService.GetSourceEffectivenessReportAsync(query);
        return BaseResult(result);
    }

    /// <summary>Xuất XLSX/CSV với đúng bộ lọc đang hiển thị.</summary>
    [HttpGet("{reportType}/export")]
    [CustomAuthorize(Enums.Menu.REPORTS, Enums.Action.READ)]
    public async Task<IActionResult> ExportReportAsync(
        string reportType,
        [FromQuery] string format,
        [FromQuery] DashboardQuery query)
    {
        var file = await _dashboardService.ExportReportAsync(reportType, format, query);
        if (file == null)
            return BadRequest(new { message = "Loại báo cáo hoặc dữ liệu xuất không hợp lệ." });

        return File(file.Content, file.ContentType, file.FileName);
    }
}
