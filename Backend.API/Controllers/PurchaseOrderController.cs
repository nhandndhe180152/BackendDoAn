using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.PurchaseOrders;
using Backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers;

[Authorize]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/purchase-orders")]
[ApiController]
public class PurchaseOrderController : BaseController
{
    private readonly IPurchaseOrderService _purchaseOrderService;

    public PurchaseOrderController(IPurchaseOrderService purchaseOrderService)
    {
        _purchaseOrderService = purchaseOrderService;
    }

    // ── Queries ─────────────────────────────────────────────────────────

    /// <summary>Phân trang danh sách đơn mua.</summary>
    [HttpPost("paged")]
    public async Task<IActionResult> GetPagedAsync([FromBody] PurchaseOrderPagedQuery query)
    {
        var result = await _purchaseOrderService.GetPagedAsync(query);
        return BaseResult(result);
    }

    /// <summary>Chi tiết đơn mua gồm items + danh sách phiếu nhập liên kết.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var result = await _purchaseOrderService.GetByIdAsync(id);
        return BaseResult(result);
    }

    // ── Commands ─────────────────────────────────────────────────────────

    /// <summary>
    /// Tạo đơn mua mới. BE tự tính TotalAmount = Σ(qty × unitCost).
    /// POCode được sinh tự động theo format PO-YYYYMMDD-XXXX.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreatePurchaseOrderDto dto)
    {
        dto.CreatedBy = this.GetLoggedInUserId();
        var result = await _purchaseOrderService.CreateAsync(dto);
        return BaseResult(result);
    }

    /// <summary>Cập nhật đơn mua — chỉ khi ở trạng thái Draft.</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdatePurchaseOrderDto dto)
    {
        dto.Id        = id;
        dto.UpdatedBy = this.GetLoggedInUserId();
        var result = await _purchaseOrderService.UpdateAsync(dto);
        return BaseResult(result);
    }

    /// <summary>Xác nhận đơn mua: Draft → Confirmed.</summary>
    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> ConfirmAsync(int id)
    {
        var result = await _purchaseOrderService.ConfirmAsync(id);
        return BaseResult(result);
    }

    /// <summary>
    /// Tạo InboundOrder từ PurchaseOrder (SourceType=PO).
    /// PO phải ở Confirmed hoặc PartiallyReceived.
    /// Chỉ tạo dòng cho những sản phẩm còn chưa nhận đủ.
    /// </summary>
    [HttpPost("{id}/create-inbound")]
    public async Task<IActionResult> CreateInboundAsync(int id)
    {
        var result = await _purchaseOrderService.CreateInboundAsync(id);
        return BaseResult(result);
    }

    /// <summary>Hủy đơn mua — chỉ khi Draft hoặc Confirmed.</summary>
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelAsync(int id)
    {
        var result = await _purchaseOrderService.CancelAsync(id);
        return BaseResult(result);
    }
}
