using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.Constants;
using Backend.Application.DTOs.InboundOrders;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers;

[Authorize]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/inbound-orders")]
[ApiController]
public class InboundOrderController : BaseController
{
    private readonly IInboundOrderService _inboundOrderService;

    public InboundOrderController(IInboundOrderService inboundOrderService)
    {
        _inboundOrderService = inboundOrderService;
    }

    private bool IsManagerOrAdmin()
    {
        var roles = this.GetLoggedInRoleIds();
        return roles.Contains(CommonConstants.Role.ADMIN);
    }

    [HttpGet]
    public async Task<IActionResult> GetPagedAsync([FromQuery] SearchQuery query)
    {
        var result = await _inboundOrderService.GetPagedAsync(query);
        return BaseResult(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var result = await _inboundOrderService.GetByIdAsync(id);
        return BaseResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateInboundOrderDto dto)
    {
        if (!IsManagerOrAdmin())
        {
            return BaseResult(ApiResponse.Forbidden("Chỉ có Warehouse Manager hoặc System Admin mới có quyền tạo phiếu nhập.", ApiCodeConstants.Common.Forbidden));
        }

        dto.CreatedBy = this.GetLoggedInUserId();
        var result = await _inboundOrderService.CreateAsync(dto);
        return BaseResult(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateInboundOrderDto dto)
    {
        if (!IsManagerOrAdmin())
        {
            return BaseResult(ApiResponse.Forbidden("Chỉ có Warehouse Manager hoặc System Admin mới có quyền chỉnh sửa phiếu nhập.", ApiCodeConstants.Common.Forbidden));
        }

        dto.Id = id;
        dto.UpdatedBy = this.GetLoggedInUserId();
        var result = await _inboundOrderService.UpdateAsync(dto);
        return BaseResult(result);
    }

    [HttpPost("{id}/submit")]
    public async Task<IActionResult> SubmitAsync(int id)
    {
        var result = await _inboundOrderService.SubmitAsync(id);
        return BaseResult(result);
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApproveAsync(int id)
    {
        if (!IsManagerOrAdmin())
        {
            return BaseResult(ApiResponse.Forbidden("Chỉ có Warehouse Manager hoặc System Admin mới có quyền duyệt phiếu nhập.", ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.ApproveAsync(id);
        return BaseResult(result);
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectAsync(int id, [FromBody] string reason)
    {
        if (!IsManagerOrAdmin())
        {
            return BaseResult(ApiResponse.Forbidden("Chỉ có Warehouse Manager hoặc System Admin mới có quyền từ chối phiếu nhập.", ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.RejectAsync(id, reason);
        return BaseResult(result);
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelAsync(int id)
    {
        if (!IsManagerOrAdmin())
        {
            return BaseResult(ApiResponse.Forbidden("Chỉ có Warehouse Manager hoặc System Admin mới có quyền hủy phiếu nhập.", ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.CancelAsync(id);
        return BaseResult(result);
    }

    // Receiving sequence
    [HttpPost("{id}/receipts/start")]
    public async Task<IActionResult> StartReceiptAsync(int id, [FromBody] StartReceiptDto dto)
    {
        var result = await _inboundOrderService.StartReceiptAsync(id, dto);
        return BaseResult(result);
    }

    [HttpPost("{id}/receipts/{receiptId}/scan-qr")]
    public async Task<IActionResult> ScanQrAsync(int id, int receiptId, [FromBody] ScanQrDto dto)
    {
        var result = await _inboundOrderService.ScanQrAsync(id, receiptId, dto);
        return BaseResult(result);
    }

    [HttpPost("{id}/receipts/{receiptId}/record-quantity")]
    public async Task<IActionResult> RecordQuantityAsync(int id, int receiptId, [FromBody] RecordQuantityDto dto)
    {
        var result = await _inboundOrderService.RecordQuantityAsync(id, receiptId, dto);
        return BaseResult(result);
    }

    [HttpPost("{id}/receipts/{receiptId}/attach-weight")]
    public async Task<IActionResult> AttachWeightAsync(int id, int receiptId, [FromBody] AttachWeightDto dto)
    {
        var result = await _inboundOrderService.AttachWeightAsync(id, receiptId, dto);
        return BaseResult(result);
    }

    [HttpPost("{id}/receipts/{receiptId}/review-exception")]
    public async Task<IActionResult> ReviewExceptionAsync(int id, int receiptId, [FromBody] ReviewExceptionDto dto)
    {
        if (!IsManagerOrAdmin())
        {
            return BaseResult(ApiResponse.Forbidden("Chỉ có Warehouse Manager hoặc System Admin mới có quyền duyệt ngoại lệ.", ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.ReviewExceptionAsync(id, receiptId, dto);
        return BaseResult(result);
    }

    [HttpGet("{id}/receipts/{receiptId}/putaway-suggestions")]
    public async Task<IActionResult> GetPutawaySuggestionsAsync(int id, int receiptId)
    {
        var result = await _inboundOrderService.GetPutawaySuggestionsAsync(id, receiptId);
        return BaseResult(result);
    }

    [HttpPost("{id}/receipts/{receiptId}/select-putaway")]
    public async Task<IActionResult> SelectPutawayAsync(int id, int receiptId, [FromBody] SelectPutawayDto dto)
    {
        var result = await _inboundOrderService.SelectPutawayAsync(id, receiptId, dto);
        return BaseResult(result);
    }

    [HttpPost("{id}/receipts/{receiptId}/confirm")]
    public async Task<IActionResult> ConfirmReceiptAsync(int id, int receiptId, [FromBody] ConfirmReceiptDto dto)
    {
        var result = await _inboundOrderService.ConfirmReceiptAsync(id, receiptId, dto);
        return BaseResult(result);
    }

    [HttpGet("{id}/receipts")]
    public async Task<IActionResult> GetReceiptsAsync(int id)
    {
        var result = await _inboundOrderService.GetReceiptsAsync(id);
        return BaseResult(result);
    }

    // Chứng từ giao hàng (Delivery Note): ảnh upload sẵn qua /file-manager/upload-by-category, ở đây gắn OriginalImageFileId
    [HttpPost("{id}/delivery-note")]
    public async Task<IActionResult> SaveDeliveryNoteAsync(int id, [FromBody] SaveDeliveryNoteDto dto)
    {
        var result = await _inboundOrderService.SaveDeliveryNoteAsync(id, dto);
        return BaseResult(result);
    }

    [HttpGet("{id}/delivery-note")]
    public async Task<IActionResult> GetDeliveryNoteAsync(int id)
    {
        var result = await _inboundOrderService.GetDeliveryNoteAsync(id);
        return BaseResult(result);
    }
}
