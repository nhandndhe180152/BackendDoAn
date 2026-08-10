using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.Constants;
using Backend.Application.DTOs.SalesOrders;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend.Domain.Enums;

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

    // ── Queries ─────────────────────────────────────────────────────────

    [HttpPost("paged")]
    [CustomAuthorize(Enums.Menu.SALE_ORDERS, Enums.Action.READ)]
    public async Task<IActionResult> GetPagedAsync([FromBody] SalesOrderPagedQuery query)
    {
        var result = await _salesOrderService.GetPagedAsync(query);
        return BaseResult(result);
    }

    [HttpGet("{id}")]
    [CustomAuthorize(Enums.Menu.SALE_ORDERS, Enums.Action.READ)]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var result = await _salesOrderService.GetByIdAsync(id);
        return BaseResult(result);
    }

    // ── Commands ─────────────────────────────────────────────────────────

    /// <summary>Tạo đơn bán mới. BE tự tính TotalAmount, không nhận từ FE.</summary>
    [HttpPost]
    [CustomAuthorize(Enums.Menu.SALE_ORDERS, Enums.Action.CREATE)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateSalesOrderDto dto)
    {
        dto.CreatedBy = this.GetLoggedInUserId();
        var result = await _salesOrderService.CreateAsync(dto);
        return BaseResult(result);
    }

    /// <summary>Cập nhật đơn bán (chỉ khi ở trạng thái NEW).</summary>
    [HttpPut("{id}")]
    [CustomAuthorize(Enums.Menu.SALE_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateSalesOrderDto dto)
    {
        dto.Id        = id;
        dto.UpdatedBy = this.GetLoggedInUserId();
        var result = await _salesOrderService.UpdateAsync(dto);
        return BaseResult(result);
    }

    /// <summary>Xác nhận đơn bán: NEW → PENDING_CONFIRM.</summary>
    [HttpPost("{id}/confirm")]
    [CustomAuthorize(Enums.Menu.SALE_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> ConfirmAsync(int id)
    {
        var result = await _salesOrderService.ConfirmAsync(id);
        return BaseResult(result);
    }

    /// <summary>
    /// Giữ hàng: PENDING_CONFIRM → RESERVED.
    /// Kiểm tra khách hàng, hạn mức công nợ, tồn khả dụng (chưa khóa QuantityReserved).
    /// QuantityReserved sẽ được khóa tại bước Allocate của phiếu xuất.
    /// </summary>
    [HttpPost("{id}/reserve")]
    [CustomAuthorize(Enums.Menu.SALE_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> ReserveAsync(int id)
    {
        var result = await _salesOrderService.ReserveAsync(id);
        return BaseResult(result);
    }

    /// <summary>Hủy đơn bán. QuantityReserved do OutboundOrder quản lý — không giải phóng tại đây.</summary>
    [HttpPost("{id}/cancel")]
    [CustomAuthorize(Enums.Menu.SALE_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> CancelAsync(int id)
    {
        var result = await _salesOrderService.CancelAsync(id);
        return BaseResult(result);
    }

    /// <summary>
    /// Tạo OutboundOrder từ SalesOrder (RESERVED/PREPARING → PREPARING + OutboundOrder DRAFT).
    /// </summary>
    [HttpPost("{id}/create-outbound")]
    [CustomAuthorize(Enums.Menu.SALE_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> CreateOutboundAsync(int id, [FromBody] CreateOutboundDto dto)
    {
        var result = await _salesOrderService.CreateOutboundAsync(id, dto);
        return BaseResult(result);
    }
}
