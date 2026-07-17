using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.Constants;
using Backend.Application.DTOs.SalesOrders;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers;

[Authorize]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/sales-orders")]
[ApiController]
public class SalesOrderController : BaseController
{
    private readonly ISalesOrderService _salesOrderService;

    public SalesOrderController(ISalesOrderService salesOrderService)
    {
        _salesOrderService = salesOrderService;
    }

    private bool IsManagerOrAdmin()
        => this.GetLoggedInRoleIds().Contains(CommonConstants.Role.ADMIN);

    // ── Queries ─────────────────────────────────────────────────────────

    [HttpPost("paged")]
    public async Task<IActionResult> GetPagedAsync([FromBody] SalesOrderPagedQuery query)
    {
        var result = await _salesOrderService.GetPagedAsync(query);
        return BaseResult(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var result = await _salesOrderService.GetByIdAsync(id);
        return BaseResult(result);
    }

    // ── Commands ─────────────────────────────────────────────────────────

    /// <summary>Tạo đơn bán mới. BE tự tính TotalAmount, không nhận từ FE.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateSalesOrderDto dto)
    {
        dto.CreatedBy = this.GetLoggedInUserId();
        var result = await _salesOrderService.CreateAsync(dto);
        return BaseResult(result);
    }

    /// <summary>Cập nhật đơn bán (chỉ khi ở trạng thái NEW).</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateSalesOrderDto dto)
    {
        dto.Id        = id;
        dto.UpdatedBy = this.GetLoggedInUserId();
        var result = await _salesOrderService.UpdateAsync(dto);
        return BaseResult(result);
    }

    /// <summary>Xác nhận đơn bán: NEW → PENDING_CONFIRM.</summary>
    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> ConfirmAsync(int id)
    {
        var result = await _salesOrderService.ConfirmAsync(id);
        return BaseResult(result);
    }

    /// <summary>
    /// Giữ hàng: PENDING_CONFIRM → RESERVED.
    /// Kiểm tra khách hàng, hạn mức công nợ, tồn khả dụng và tăng QuantityReserved.
    /// </summary>
    [HttpPost("{id}/reserve")]
    public async Task<IActionResult> ReserveAsync(int id)
    {
        var result = await _salesOrderService.ReserveAsync(id);
        return BaseResult(result);
    }

    /// <summary>Hủy đơn bán. Giải phóng tồn giữ nếu đang RESERVED.</summary>
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelAsync(int id)
    {
        var result = await _salesOrderService.CancelAsync(id);
        return BaseResult(result);
    }

    /// <summary>
    /// Tạo OutboundOrder từ SalesOrder (RESERVED/PREPARING → PREPARING + OutboundOrder DRAFT).
    /// </summary>
    [HttpPost("{id}/create-outbound")]
    public async Task<IActionResult> CreateOutboundAsync(int id)
    {
        var result = await _salesOrderService.CreateOutboundAsync(id);
        return BaseResult(result);
    }
}
