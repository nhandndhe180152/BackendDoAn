using Asp.Versioning;
using Backend.Application.DTOs.OutboundOrders;
using Backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers;

[Authorize]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/outbound-orders")]
[ApiController]
public class OutboundOrderController : BaseController
{
    private readonly IOutboundOrderService _outboundOrderService;

    public OutboundOrderController(IOutboundOrderService outboundOrderService)
    {
        _outboundOrderService = outboundOrderService;
    }

    // ── Queries ─────────────────────────────────────────────────────────

    [HttpPost("paged")]
    public async Task<IActionResult> GetPagedAsync([FromBody] OutboundOrderPagedQuery query)
    {
        var result = await _outboundOrderService.GetPagedAsync(query);
        return BaseResult(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var result = await _outboundOrderService.GetByIdAsync(id);
        return BaseResult(result);
    }

    // ── Commands ─────────────────────────────────────────────────────────

    /// <summary>
    /// Phân bổ lot/vị trí kho cho từng dòng sản phẩm → PICKING.
    /// </summary>
    [HttpPost("{id}/allocate")]
    public async Task<IActionResult> AllocateAsync(int id, [FromBody] AllocateOutboundDto dto)
    {
        var result = await _outboundOrderService.AllocateAsync(id, dto);
        return BaseResult(result);
    }

    /// <summary>
    /// Cập nhật số lượng thực tế đã lấy cho từng allocation.
    /// </summary>
    [HttpPost("{id}/pick")]
    public async Task<IActionResult> PickAsync(int id, [FromBody] PickOutboundDto dto)
    {
        var result = await _outboundOrderService.PickAsync(id, dto);
        return BaseResult(result);
    }

    /// <summary>
    /// Xác nhận đóng gói xong → PACKED. Validate tất cả items đã pick đủ.
    /// </summary>
    [HttpPost("{id}/confirm-packing")]
    public async Task<IActionResult> ConfirmPackingAsync(int id)
    {
        var result = await _outboundOrderService.ConfirmPackingAsync(id);
        return BaseResult(result);
    }

    /// <summary>
    /// Xác nhận xuất kho (DISPATCHED) — bước quan trọng nhất:
    /// Giảm tồn, tạo InventoryTransaction per lot, cập nhật SalesOrder → DELIVERING,
    /// tạo PartyDebt/DebtTransaction nếu khách chưa trả đủ.
    /// </summary>
    [HttpPost("{id}/confirm-dispatch")]
    public async Task<IActionResult> ConfirmDispatchAsync(int id, [FromBody] ConfirmDispatchDto dto)
    {
        var result = await _outboundOrderService.ConfirmDispatchAsync(id, dto);
        return BaseResult(result);
    }

    /// <summary>
    /// Hủy phiếu xuất. Giải phóng QuantityReserved nếu đang PICKING/PACKED.
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelAsync(int id)
    {
        var result = await _outboundOrderService.CancelAsync(id);
        return BaseResult(result);
    }
}
