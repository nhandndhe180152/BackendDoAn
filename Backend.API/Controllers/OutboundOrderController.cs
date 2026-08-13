using Asp.Versioning;
using Backend.Application.DTOs.OutboundOrders;
using Backend.Application.Interfaces;
using Backend.Application.Constants;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend.API.Utilities;
using Backend.Domain.Enums;

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
    [CustomAuthorize(Enums.Menu.OUTBOUND_ORDERS, Enums.Action.READ)]
    public async Task<IActionResult> GetPagedAsync([FromBody] OutboundOrderPagedQuery query)
    {
        var result = await _outboundOrderService.GetPagedAsync(query);
        return BaseResult(result);
    }

    [HttpGet("{id}")]
    [CustomAuthorize(Enums.Menu.OUTBOUND_ORDERS, Enums.Action.READ)]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var result = await _outboundOrderService.GetByIdAsync(id);
        return BaseResult(result);
    }

    [HttpGet("{id}/allocation-candidates")]
    [CustomAuthorize(Enums.Menu.OUTBOUND_ORDERS, Enums.Action.READ)]
    public async Task<IActionResult> GetAllocationCandidatesAsync(int id)
    {
        var result = await _outboundOrderService.GetAllocationCandidatesAsync(id);
        return BaseResult(result);
    }

    // ── Commands ─────────────────────────────────────────────────────────

    /// <summary>
    /// Phân bổ lot/vị trí kho cho từng dòng sản phẩm → PICKING.
    /// </summary>
    [HttpPost("{id}/allocate")]
    [CustomAuthorize(Enums.Menu.OUTBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> AllocateAsync(int id, [FromBody] AllocateOutboundDto dto)
    {
        var result = await _outboundOrderService.AllocateAsync(id, dto);
        return BaseResult(result);
    }

    /// <summary>
    /// Cập nhật số lượng thực tế đã lấy cho từng allocation.
    /// </summary>
    [HttpPost("{id}/pick")]
    [CustomAuthorize(Enums.Menu.OUTBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> PickAsync(int id, [FromBody] PickOutboundDto dto)
    {
        var result = await _outboundOrderService.PickAsync(id, dto);
        return BaseResult(result);
    }

    /// <summary>
    /// Xác nhận đóng gói xong → PACKED. Validate tất cả items đã pick đủ.
    /// </summary>
    [HttpPost("{id}/confirm-packing")]
    [CustomAuthorize(Enums.Menu.OUTBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> ConfirmPackingAsync(int id, [FromBody] Backend.Application.DTOs.OutboundOrders.ConfirmPackingDto dto)
    {
        var result = await _outboundOrderService.ConfirmPackingAsync(id, dto);
        return BaseResult(result);
    }

    /// <summary>
    /// Xác nhận xuất kho (DISPATCHED) — bước quan trọng nhất:
    /// Giảm tồn, tạo InventoryTransaction per lot, cập nhật SalesOrder → DELIVERING,
    /// tạo PartyDebt/DebtTransaction nếu khách chưa trả đủ.
    /// </summary>
    [HttpPost("{id}/confirm-dispatch")]
    [CustomAuthorize(Enums.Menu.OUTBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> ConfirmDispatchAsync(int id, [FromBody] ConfirmDispatchDto dto)
    {
        var result = await _outboundOrderService.ConfirmDispatchAsync(id, dto);
        return BaseResult(result);
    }

    /// <summary>
    /// Xác nhận giao hàng thành công (DISPATCHED → COMPLETED).
    /// </summary>
    [HttpPost("{id}/complete-delivery")]
    [CustomAuthorize(Enums.Menu.OUTBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> CompleteDeliveryAsync(int id, [FromBody] CompleteDeliveryDto dto)
    {
        var result = await _outboundOrderService.CompleteDeliveryAsync(id, dto);
        return BaseResult(result);
    }

    /// <summary>
    /// Xác nhận giao hàng thất bại (DISPATCHED → DELIVERY_FAILED).
    /// </summary>
    [HttpPost("{id}/fail-delivery")]
    [CustomAuthorize(Enums.Menu.OUTBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> FailDeliveryAsync(int id, [FromBody] FailDeliveryDto dto)
    {
        var result = await _outboundOrderService.FailDeliveryAsync(id, dto);
        return BaseResult(result);
    }

    /// <summary>
    /// Hủy phiếu xuất kèm lý do (bắt buộc, validate bởi CancelOutboundOrderDtoValidator).
    /// Giải phóng QuantityReserved nếu đang PICKING/PACKED.
    /// </summary>
    [HttpPost("{id}/cancel")]
    [CustomAuthorize(Enums.Menu.OUTBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> CancelAsync(int id, [FromBody] CancelOutboundOrderDto dto)
    {
        var result = await _outboundOrderService.CancelAsync(id, dto.Reason);
        return BaseResult(result);
    }

    [HttpPost("{id}/force-unlock")]
    [CustomAuthorize(Enums.Menu.OUTBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> ForceUnlockAsync(int id, [FromBody] ForceUnlockOutboundDto dto)
    {
        var roles = this.GetLoggedInRoleIds();
        if (!roles.Contains(CommonConstants.Role.ADMIN) && !roles.Contains(CommonConstants.Role.OWNER))
            return BaseResult(ApiResponse.Forbidden(message: "Chỉ ADMIN/OWNER được mở khóa cột thủ công."));
        var result = await _outboundOrderService.ForceUnlockAsync(id, dto.Reason);
        return BaseResult(result);
    }
}
