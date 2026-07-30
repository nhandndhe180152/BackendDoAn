using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.Constants;
using Backend.Application.DTOs.InboundOrders;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Backend.Domain.Enums;

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
        return roles.Contains(CommonConstants.Role.ADMIN) ||
               roles.Contains(CommonConstants.Role.OWNER);
    }

    private bool IsInboundOperator()
    {
        var roles = this.GetLoggedInRoleIds();
        return roles.Contains(CommonConstants.Role.ADMIN) ||
               roles.Contains(CommonConstants.Role.OWNER) ||
               roles.Contains(CommonConstants.Role.WAREHOUSE);
    }

    [HttpGet]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.READ)]
    public async Task<IActionResult> GetPagedAsync([FromQuery] SearchQuery query)
    {
        var result = await _inboundOrderService.GetPagedAsync(query);
        return BaseResult(result);
    }

    /// API phân trang nâng cao (DataTables) cho màn quản lý phiếu nhập: lọc theo cột + sắp xếp
    [HttpPost("paged-advanced")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.READ)]
    public async Task<IActionResult> GetPagedAdvancedAsync([FromBody] InboundOrderDTParameters parameters)
    {
        var result = await _inboundOrderService.GetPagedAdvancedAsync(parameters);
        return BaseResult(result);
    }

    /// Danh sách phiếu nhập lúa/gạo đang chờ xếp kho — 1 request thay cho list + N getById ở màn Store-in.
    [HttpGet("putaway-pending")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.READ)]
    public async Task<IActionResult> GetPutawayPendingAsync()
    {
        if (!IsInboundOperator())
        {
            return BaseResult(ApiResponse.Forbidden("Vai trò hiện tại không có quyền xử lý xếp kho.", ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.GetPutawayPendingAsync();
        return BaseResult(result);
    }

    [HttpGet("{id}")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.READ)]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var result = await _inboundOrderService.GetByIdAsync(id);
        return BaseResult(result);
    }

    [HttpPost]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.CREATE)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateInboundOrderDto dto)
    {
        if (!IsManagerOrAdmin())
        {
            return BaseResult(ApiResponse.Forbidden(message: "Chỉ có Warehouse Manager hoặc System Admin mới có quyền tạo phiếu nhập.", code: ApiCodeConstants.Common.Forbidden));
        }

        dto.CreatedBy = this.GetLoggedInUserId();
        var result = await _inboundOrderService.CreateAsync(dto);
        return BaseResult(result);
    }

    [HttpPut("{id}")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateInboundOrderDto dto)
    {
        if (!IsManagerOrAdmin())
        {
            return BaseResult(ApiResponse.Forbidden(message: "Chỉ có Warehouse Manager hoặc System Admin mới có quyền chỉnh sửa phiếu nhập.", code: ApiCodeConstants.Common.Forbidden));
        }

        dto.Id = id;
        dto.UpdatedBy = this.GetLoggedInUserId();
        var result = await _inboundOrderService.UpdateAsync(dto);
        return BaseResult(result);
    }

    [HttpPost("{id}/submit")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> SubmitAsync(int id)
    {
        if (!IsInboundOperator())
        {
            return BaseResult(ApiResponse.Forbidden(message: "Chỉ Quản trị viên, Chủ kho hoặc Nhân viên kho mới có quyền gửi duyệt phiếu nhập.", code: ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.SubmitAsync(id);
        return BaseResult(result);
    }

    [HttpPost("{id}/approve")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> ApproveAsync(int id)
    {
        if (!IsManagerOrAdmin())
        {
            return BaseResult(ApiResponse.Forbidden(message: "Chỉ có Warehouse Manager hoặc System Admin mới có quyền duyệt phiếu nhập.", code: ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.ApproveAsync(id);
        return BaseResult(result);
    }

    [HttpPost("{id}/reject")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> RejectAsync(int id, [FromBody] string reason)
    {
        if (!IsManagerOrAdmin())
        {
            return BaseResult(ApiResponse.Forbidden(message: "Chỉ có Warehouse Manager hoặc System Admin mới có quyền từ chối phiếu nhập.", code: ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.RejectAsync(id, reason);
        return BaseResult(result);
    }

    [HttpPost("{id}/cancel")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> CancelAsync(int id)
    {
        if (!IsManagerOrAdmin())
        {
            return BaseResult(ApiResponse.Forbidden(message: "Chỉ có Warehouse Manager hoặc System Admin mới có quyền hủy phiếu nhập.", code: ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.CancelAsync(id);
        return BaseResult(result);
    }

    // Receiving sequence
    [HttpPost("{id}/receipts/start")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> StartReceiptAsync(int id, [FromBody] StartReceiptDto dto)
    {
        if (!IsInboundOperator())
        {
            return BaseResult(ApiResponse.Forbidden(message: "Chỉ Quản trị viên, Chủ kho hoặc Nhân viên kho mới có quyền bắt đầu nhận hàng.", code: ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.StartReceiptAsync(id, dto);
        return BaseResult(result);
    }

    [HttpPost("{id}/receipts/{receiptId}/scan-qr")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> ScanQrAsync(int id, int receiptId, [FromBody] ScanQrDto dto)
    {
        if (!IsInboundOperator())
        {
            return BaseResult(ApiResponse.Forbidden(message: "Vai trò hiện tại không có quyền quét nhận hàng.", code: ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.ScanQrAsync(id, receiptId, dto);
        return BaseResult(result);
    }

    [HttpPost("{id}/receipts/{receiptId}/record-quantity")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> RecordQuantityAsync(int id, int receiptId, [FromBody] RecordQuantityDto dto)
    {
        if (!IsInboundOperator())
        {
            return BaseResult(ApiResponse.Forbidden(message: "Vai trò hiện tại không có quyền ghi nhận số lượng nhập.", code: ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.RecordQuantityAsync(id, receiptId, dto);
        return BaseResult(result);
    }

    [HttpPost("{id}/receipts/{receiptId}/attach-weight")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> AttachWeightAsync(int id, int receiptId, [FromBody] AttachWeightDto dto)
    {
        if (!IsInboundOperator())
        {
            return BaseResult(ApiResponse.Forbidden(message: "Vai trò hiện tại không có quyền gắn bằng chứng cân.", code: ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.AttachWeightAsync(id, receiptId, dto);
        return BaseResult(result);
    }

    [HttpPost("{id}/receipts/{receiptId}/review-exception")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> ReviewExceptionAsync(int id, int receiptId, [FromBody] ReviewExceptionDto dto)
    {
        if (!IsManagerOrAdmin())
        {
            return BaseResult(ApiResponse.Forbidden(message: "Chỉ có Warehouse Manager hoặc System Admin mới có quyền duyệt ngoại lệ.", code: ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.ReviewExceptionAsync(id, receiptId, dto);
        return BaseResult(result);
    }

    [HttpGet("{id}/receipts/{receiptId}/putaway-suggestions")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.READ)]
    public async Task<IActionResult> GetPutawaySuggestionsAsync(int id, int receiptId)
    {
        if (!IsInboundOperator())
        {
            return BaseResult(ApiResponse.Forbidden(message: "Vai trò hiện tại không có quyền xử lý gợi ý xếp kho.", code: ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.GetPutawaySuggestionsAsync(id, receiptId);
        return BaseResult(result);
    }

    [HttpPost("{id}/receipts/{receiptId}/select-putaway")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> SelectPutawayAsync(int id, int receiptId, [FromBody] SelectPutawayDto dto)
    {
        if (!IsInboundOperator())
        {
            return BaseResult(ApiResponse.Forbidden(message: "Vai trò hiện tại không có quyền chọn vị trí xếp kho.", code: ApiCodeConstants.Common.Forbidden));
        }
        if (dto.IsOverride && !IsManagerOrAdmin())
        {
            return BaseResult(ApiResponse.Forbidden(message: "Chỉ Quản trị viên hoặc Chủ kho mới có quyền ghi đè vị trí đề xuất.", code: ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.SelectPutawayAsync(id, receiptId, dto);
        return BaseResult(result);
    }

    [HttpPost("{id}/receipts/{receiptId}/confirm")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> ConfirmReceiptAsync(int id, int receiptId, [FromBody] ConfirmReceiptDto dto)
    {
        if (!IsInboundOperator())
        {
            return BaseResult(ApiResponse.Forbidden(message: "Vai trò hiện tại không có quyền xác nhận nhập kho.", code: ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.ConfirmReceiptAsync(id, receiptId, dto);
        return BaseResult(result);
    }

    /// <summary>Gap 3: Đảo ngược một dòng phiếu nhập đã xác nhận nhập kho (store-in sai).</summary>
    [HttpPost("{id}/receipts/{receiptId}/reverse")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> ReverseReceiptAsync(int id, int receiptId, [FromQuery] string reason)
    {
        if (!IsManagerOrAdmin())
        {
            return BaseResult(ApiResponse.Forbidden(message: "Chỉ Quản trị viên hoặc Chủ kho mới có quyền đảo ngược nhập kho.", code: ApiCodeConstants.Common.Forbidden));
        }

        var result = await _inboundOrderService.ReverseReceiptAsync(id, receiptId, reason);
        return BaseResult(result);
    }

    [HttpGet("{id}/receipts")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.READ)]
    public async Task<IActionResult> GetReceiptsAsync(int id)
    {
        var result = await _inboundOrderService.GetReceiptsAsync(id);
        return BaseResult(result);
    }

    // Chứng từ giao hàng (Delivery Note): ảnh upload sẵn qua /file-manager/upload-by-category, ở đây gắn OriginalImageFileId
    [HttpPost("{id}/delivery-note")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> SaveDeliveryNoteAsync(int id, [FromBody] SaveDeliveryNoteDto dto)
    {
        var result = await _inboundOrderService.SaveDeliveryNoteAsync(id, dto);
        return BaseResult(result);
    }

    [HttpGet("{id}/delivery-note")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.READ)]
    public async Task<IActionResult> GetDeliveryNoteAsync(int id)
    {
        var result = await _inboundOrderService.GetDeliveryNoteAsync(id);
        return BaseResult(result);
    }

    // ── Non-paddy (PO → InboundOrder) flow ──────────────────────────────────────

    /// <summary>
    /// Ghi số lượng thực nhận cho từng dòng phiếu nhập (non-paddy / SourceType=PO).
    /// Có thể gọi nhiều đợt — chỉ cập nhật QuantityReceived, chưa tăng tồn kho.
    /// </summary>
    [HttpPost("{id}/non-paddy/receive")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> ReceiveNonPaddyAsync(int id, [FromBody] Backend.Application.DTOs.InboundOrders.ReceiveNonPaddyDto dto)
    {
        var result = await _inboundOrderService.ReceiveNonPaddyAsync(id, dto);
        return BaseResult(result);
    }

    /// <summary>
    /// Xác nhận hoàn tất nhập kho non-paddy trong 1 DB transaction:
    /// tăng QuantityOnHand, tạo InventoryTransaction per dòng,
    /// và cập nhật PurchaseOrder → PartiallyReceived / Received.
    /// </summary>
    [HttpPost("{id}/non-paddy/confirm")]
    [CustomAuthorize(Enums.Menu.INBOUND_ORDERS, Enums.Action.UPDATE)]
    public async Task<IActionResult> ConfirmNonPaddyReceiveAsync(int id, [FromBody] Backend.Application.DTOs.InboundOrders.ConfirmNonPaddyReceiveDto dto)
    {
        var result = await _inboundOrderService.ConfirmNonPaddyReceiveAsync(id, dto);
        return BaseResult(result);
    }
}
