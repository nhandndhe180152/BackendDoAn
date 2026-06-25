using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DependencyInjection.Extentions;
using Backend.Application.DTOs.InboundOrders;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Domain.Abstractions.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Backend.Share.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Application.Implements;

public class InboundOrderService : IInboundOrderService
{
    private readonly IRepositoryBase<InboundOrder, int> _inboundOrderRepository;
    private readonly IInboundOrderItemRepository _inboundOrderItemRepository;
    private readonly IRepositoryBase<InboundOrderStatus, int> _inboundOrderStatusRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IRepositoryBase<Supplier, int> _supplierRepository;
    private readonly IProductVariantRepository _productVariantRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryTransactionRepository _inventoryTransactionRepository;
    private readonly IIotWeightLogRepository _iotWeightLogRepository;
    private readonly IIotDeviceRepository _iotDeviceRepository;
    private readonly ISystemConfigRepository _systemConfigRepository;
    private readonly IRepositoryBase<AuditLog, int> _auditLogRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<InboundOrderService> _logger;

    public InboundOrderService(
        IRepositoryBase<InboundOrder, int> inboundOrderRepository,
        IInboundOrderItemRepository inboundOrderItemRepository,
        IRepositoryBase<InboundOrderStatus, int> inboundOrderStatusRepository,
        IWarehouseRepository warehouseRepository,
        IRepositoryBase<Supplier, int> supplierRepository,
        IProductVariantRepository productVariantRepository,
        ILocationRepository locationRepository,
        IInventoryRepository inventoryRepository,
        IInventoryTransactionRepository inventoryTransactionRepository,
        IIotWeightLogRepository iotWeightLogRepository,
        IIotDeviceRepository iotDeviceRepository,
        ISystemConfigRepository systemConfigRepository,
        IRepositoryBase<AuditLog, int> auditLogRepository,
        IHttpContextAccessor httpContextAccessor,
        ILogger<InboundOrderService> logger)
    {
        _inboundOrderRepository = inboundOrderRepository;
        _inboundOrderItemRepository = inboundOrderItemRepository;
        _inboundOrderStatusRepository = inboundOrderStatusRepository;
        _warehouseRepository = warehouseRepository;
        _supplierRepository = supplierRepository;
        _productVariantRepository = productVariantRepository;
        _locationRepository = locationRepository;
        _inventoryRepository = inventoryRepository;
        _inventoryTransactionRepository = inventoryTransactionRepository;
        _iotWeightLogRepository = iotWeightLogRepository;
        _iotDeviceRepository = iotDeviceRepository;
        _systemConfigRepository = systemConfigRepository;
        _auditLogRepository = auditLogRepository;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private async Task<int> GetStatusIdAsync(string name)
    {
        var status = await _inboundOrderStatusRepository.FirstOrDefaultAsync(x => x.Name == name && !x.IsDeleted);
        if (status == null)
        {
            throw new InvalidOperationException($"InboundOrderStatus with name '{name}' not found.");
        }
        return status.Id;
    }

    private int GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return 0;
        return httpContext.GetCurrentUserId();
    }

    private async Task LogAuditAsync(string action, string targetType, string? targetId, string description, string? dataBefore = null, string? dataAfter = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var audit = new AuditLog
        {
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            DataBefore = dataBefore,
            DataAfter = dataAfter,
            Description = description,
            IpAddress = httpContext?.GetRemoteHostIpAddress(),
            UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString(),
            CreatedBy = GetCurrentUserId(),
            CreatedDate = DateTime.Now
        };
        await _auditLogRepository.CreateAsync(audit);
        await _auditLogRepository.SaveChangesAsync();
    }

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _inboundOrderRepository
            .FindByCondition(x => !x.IsDeleted, false, x => x.Warehouse, x => x.Supplier, x => x.InboundOrderStatus);

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword.ToLower();
            data = data.Where(x => x.POCode.ToLower().Contains(keyword) ||
                                   (x.Note != null && x.Note.ToLower().Contains(keyword)));
        }

        var totalRecord = await data.CountAsync();

        var list = await data
            .OrderByDescending(x => x.CreatedDate)
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => x.ToListDto())
            .ToListAsync();

        var pagedData = new PagingData<InboundOrderListDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = list,
            Total = totalRecord,
            TotalFiltered = totalRecord
        };

        return ApiResponse.Success(pagedData);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var order = await _inboundOrderRepository.FirstOrDefaultAsync(
            x => x.Id == id && !x.IsDeleted,
            false,
            x => x.Warehouse,
            x => x.Supplier,
            x => x.InboundOrderStatus,
            x => x.InboundOrderItems
        );

        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy InboundOrder.", ApiCodeConstants.Common.NotFound);

        // Fetch variants for items
        foreach (var item in order.InboundOrderItems)
        {
            if (item.ProductVariantId.HasValue)
            {
                item.ProductVariant = await _productVariantRepository.FirstOrDefaultAsync(v => v.Id == item.ProductVariantId.Value && !v.IsDeleted, false, v => v.Product);
            }
        }

        return ApiResponse.Success(order.ToDetailDto());
    }

    public async Task<ApiResponse> CreateAsync(CreateInboundOrderDto dto)
    {
        // Validation
        var warehouse = await _warehouseRepository.FirstOrDefaultAsync(x => x.Id == dto.WarehouseId && !x.IsDeleted && x.IsActive);
        if (warehouse == null)
            return ApiResponse.UnprocessableEntity("Kho hàng không tồn tại hoặc đã bị khóa.", ApiCodeConstants.Common.UnprocessableEntity);

        if (dto.SupplierId.HasValue)
        {
            var supplier = await _supplierRepository.FirstOrDefaultAsync(x => x.Id == dto.SupplierId.Value && !x.IsDeleted && x.IsActive);
            if (supplier == null)
                return ApiResponse.UnprocessableEntity("Nhà cung cấp không tồn tại hoặc đã bị khóa.", ApiCodeConstants.Common.UnprocessableEntity);
        }

        if (dto.Items == null || !dto.Items.Any())
            return ApiResponse.UnprocessableEntity("Vui lòng chọn ít nhất một sản phẩm.", ApiCodeConstants.Common.UnprocessableEntity);

        // Duplicate variant check
        var variantIds = dto.Items.Select(x => x.ProductVariantId).ToList();
        if (variantIds.Count != variantIds.Distinct().Count())
            return ApiResponse.UnprocessableEntity("Sản phẩm không được xuất hiện nhiều lần trong cùng một phiếu.", ApiCodeConstants.Common.UnprocessableEntity);

        foreach (var item in dto.Items)
        {
            var pv = await _productVariantRepository.FirstOrDefaultAsync(x => x.Id == item.ProductVariantId && !x.IsDeleted && x.IsActive);
            if (pv == null)
                return ApiResponse.UnprocessableEntity($"Sản phẩm variant {item.ProductVariantId} không tồn tại hoặc đã bị khóa.", ApiCodeConstants.Common.UnprocessableEntity);

            if (item.QuantityOrdered <= 0)
                return ApiResponse.UnprocessableEntity("Số lượng đặt phải lớn hơn 0.", ApiCodeConstants.Common.UnprocessableEntity);

            if (item.UnitCostPrice < 0)
                return ApiResponse.UnprocessableEntity("Giá nhập không được âm.", ApiCodeConstants.Common.UnprocessableEntity);
        }

        // Generate PO Code
        var todayStr = DateTimeHelper.VietnamNow().ToString("yyyyMMdd");
        var countToday = await _inboundOrderRepository.CountByConditionAsync(x => x.POCode.StartsWith("PO-" + todayStr));
        var nextNum = countToday + 1;
        var poCode = $"PO-{todayStr}-{nextNum:D5}";

        var order = dto.ToEntity();
        order.POCode = poCode;
        order.InboundOrderStatusId = await GetStatusIdAsync(InboundOrderStatusNames.Draft);
        order.TotalAssetValue = dto.Items.Sum(x => x.QuantityOrdered * x.UnitCostPrice);

        await _inboundOrderRepository.CreateAsync(order);
        await _inboundOrderRepository.SaveChangesAsync();

        await LogAuditAsync("CREATE", nameof(InboundOrder), order.Id.ToString(), $"Tạo mới phiếu nhập kho {poCode} ở trạng thái Draft.");

        return ApiResponse.Created(order.Id);
    }

    public async Task<ApiResponse> UpdateAsync(UpdateInboundOrderDto dto)
    {
        var order = await _inboundOrderRepository.FirstOrDefaultAsync(
            x => x.Id == dto.Id && !x.IsDeleted,
            true,
            x => x.InboundOrderItems
        );

        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

        var draftStatusId = await GetStatusIdAsync(InboundOrderStatusNames.Draft);
        if (order.InboundOrderStatusId != draftStatusId)
            return ApiResponse.UnprocessableEntity("Chỉ cho phép cập nhật phiếu nhập ở trạng thái Draft.", ApiCodeConstants.Common.UnprocessableEntity);

        if (dto.SupplierId.HasValue)
        {
            var supplier = await _supplierRepository.FirstOrDefaultAsync(x => x.Id == dto.SupplierId.Value && !x.IsDeleted && x.IsActive);
            if (supplier == null)
                return ApiResponse.UnprocessableEntity("Nhà cung cấp không tồn tại hoặc đã bị khóa.", ApiCodeConstants.Common.UnprocessableEntity);
        }

        // Remove old items
        foreach (var oldItem in order.InboundOrderItems.ToList())
        {
            await _inboundOrderItemRepository.HardDeleteAsync(oldItem.Id);
        }
        order.InboundOrderItems.Clear();

        // Duplicate variant check
        var variantIds = dto.Items.Select(x => x.ProductVariantId).ToList();
        if (variantIds.Count != variantIds.Distinct().Count())
            return ApiResponse.UnprocessableEntity("Sản phẩm không được xuất hiện nhiều lần trong cùng một phiếu.", ApiCodeConstants.Common.UnprocessableEntity);

        // Add new items
        foreach (var item in dto.Items)
        {
            var pv = await _productVariantRepository.FirstOrDefaultAsync(x => x.Id == item.ProductVariantId && !x.IsDeleted && x.IsActive);
            if (pv == null)
                return ApiResponse.UnprocessableEntity($"Sản phẩm variant {item.ProductVariantId} không tồn tại hoặc đã bị khóa.", ApiCodeConstants.Common.UnprocessableEntity);

            if (item.QuantityOrdered <= 0)
                return ApiResponse.UnprocessableEntity("Số lượng đặt phải lớn hơn 0.", ApiCodeConstants.Common.UnprocessableEntity);

            if (item.UnitCostPrice < 0)
                return ApiResponse.UnprocessableEntity("Giá nhập không được âm.", ApiCodeConstants.Common.UnprocessableEntity);

            order.InboundOrderItems.Add(new InboundOrderItem
            {
                ProductVariantId = item.ProductVariantId,
                QuantityOrdered = item.QuantityOrdered,
                QuantityReceived = 0,
                UnitCostPrice = item.UnitCostPrice,
                ExpectedWeightKg = 0,
                ActualWeightKg = 0,
                QRScanned = false,
                Note = item.Note,
                CreatedBy = GetCurrentUserId(),
                CreatedDate = DateTime.Now
            });
        }

        order.SupplierId = dto.SupplierId;
        order.ExpectedDate = dto.ExpectedDate;
        order.Note = dto.Note;
        order.TotalAssetValue = dto.Items.Sum(x => x.QuantityOrdered * x.UnitCostPrice);
        order.UpdatedBy = GetCurrentUserId();
        order.LastModifiedDate = DateTime.Now;

        await _inboundOrderRepository.UpdateAsync(order);
        await _inboundOrderRepository.SaveChangesAsync();

        await LogAuditAsync("UPDATE", nameof(InboundOrder), order.Id.ToString(), $"Cập nhật thông tin phiếu nhập kho {order.POCode}.");

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> SubmitAsync(int id)
    {
        var order = await _inboundOrderRepository.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, true);
        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

        var draftId = await GetStatusIdAsync(InboundOrderStatusNames.Draft);
        if (order.InboundOrderStatusId != draftId)
            return ApiResponse.UnprocessableEntity("Chỉ phiếu nhập ở trạng thái Draft mới có thể gửi duyệt.", ApiCodeConstants.Common.UnprocessableEntity);

        order.InboundOrderStatusId = await GetStatusIdAsync(InboundOrderStatusNames.Submitted);
        order.UpdatedBy = GetCurrentUserId();
        order.LastModifiedDate = DateTime.Now;

        await _inboundOrderRepository.UpdateAsync(order);
        await _inboundOrderRepository.SaveChangesAsync();

        await LogAuditAsync("SUBMIT", nameof(InboundOrder), order.Id.ToString(), $"Gửi duyệt phiếu nhập {order.POCode}.");

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> ApproveAsync(int id)
    {
        var order = await _inboundOrderRepository.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, true);
        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

        var submittedId = await GetStatusIdAsync(InboundOrderStatusNames.Submitted);
        if (order.InboundOrderStatusId != submittedId)
            return ApiResponse.UnprocessableEntity("Chỉ phiếu nhập ở trạng thái Submitted mới có thể phê duyệt.", ApiCodeConstants.Common.UnprocessableEntity);

        order.InboundOrderStatusId = await GetStatusIdAsync(InboundOrderStatusNames.Approved);
        order.UpdatedBy = GetCurrentUserId();
        order.LastModifiedDate = DateTime.Now;

        await _inboundOrderRepository.UpdateAsync(order);
        await _inboundOrderRepository.SaveChangesAsync();

        await LogAuditAsync("APPROVE", nameof(InboundOrder), order.Id.ToString(), $"Phê duyệt phiếu nhập {order.POCode}.");

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> RejectAsync(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return ApiResponse.BadRequest("Lý do từ chối không được để trống.", ApiCodeConstants.Common.BadRequest);

        var order = await _inboundOrderRepository.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, true);
        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

        var submittedId = await GetStatusIdAsync(InboundOrderStatusNames.Submitted);
        if (order.InboundOrderStatusId != submittedId)
            return ApiResponse.UnprocessableEntity("Chỉ phiếu nhập ở trạng thái Submitted mới có thể từ chối.", ApiCodeConstants.Common.UnprocessableEntity);

        order.InboundOrderStatusId = await GetStatusIdAsync(InboundOrderStatusNames.Rejected);
        order.Note = string.IsNullOrWhiteSpace(order.Note) ? $"Từ chối: {reason}" : $"{order.Note} | Từ chối: {reason}";
        order.UpdatedBy = GetCurrentUserId();
        order.LastModifiedDate = DateTime.Now;

        await _inboundOrderRepository.UpdateAsync(order);
        await _inboundOrderRepository.SaveChangesAsync();

        await LogAuditAsync("REJECT", nameof(InboundOrder), order.Id.ToString(), $"Từ chối phiếu nhập {order.POCode}. Lý do: {reason}");

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> CancelAsync(int id)
    {
        var order = await _inboundOrderRepository.FirstOrDefaultAsync(
            x => x.Id == id && !x.IsDeleted,
            true,
            x => x.InboundOrderItems
        );

        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

        var allowedStatusNames = new[] {
            InboundOrderStatusNames.Draft,
            InboundOrderStatusNames.Submitted,
            InboundOrderStatusNames.Approved,
            InboundOrderStatusNames.Receiving
        };

        var orderStatus = await _inboundOrderStatusRepository.GetByIdAsync(order.InboundOrderStatusId);
        if (orderStatus == null || !allowedStatusNames.Contains(orderStatus.Name))
            return ApiResponse.UnprocessableEntity("Trạng thái hiện tại của phiếu nhập không cho phép hủy.", ApiCodeConstants.Common.UnprocessableEntity);

        // Check if any confirmed receipt exists. Confirmed receipt means QuantityReceived > 0
        var hasConfirmedReceipt = order.InboundOrderItems.Any(x => x.QuantityReceived > 0);
        if (hasConfirmedReceipt)
            return ApiResponse.UnprocessableEntity("Không thể hủy phiếu nhập đã có sản phẩm được xác nhận nhập kho.", ApiCodeConstants.Common.UnprocessableEntity);

        // Cancel open receipts/items states
        foreach (var item in order.InboundOrderItems)
        {
            var state = item.GetReceiptState();
            if (state.ReceiptStatus != "Confirmed")
            {
                state.ReceiptStatus = "Cancelled";
                item.SaveReceiptState(state);
                await _inboundOrderItemRepository.UpdateAsync(item);
            }
        }

        order.InboundOrderStatusId = await GetStatusIdAsync(InboundOrderStatusNames.Cancelled);
        order.UpdatedBy = GetCurrentUserId();
        order.LastModifiedDate = DateTime.Now;

        await _inboundOrderRepository.UpdateAsync(order);
        await _inboundOrderRepository.SaveChangesAsync();

        await LogAuditAsync("CANCEL", nameof(InboundOrder), order.Id.ToString(), $"Hủy phiếu nhập kho {order.POCode}.");

        return ApiResponse.Success();
    }

    // Receiving sequence
    public async Task<ApiResponse> StartReceiptAsync(int orderId, StartReceiptDto dto)
    {
        var order = await _inboundOrderRepository.FirstOrDefaultAsync(
            x => x.Id == orderId && !x.IsDeleted,
            true,
            x => x.Warehouse,
            x => x.InboundOrderStatus
        );

        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

        var allowedStatusNames = new[] { InboundOrderStatusNames.Approved, InboundOrderStatusNames.Receiving };
        if (!allowedStatusNames.Contains(order.InboundOrderStatus.Name))
            return ApiResponse.UnprocessableEntity("Phiếu nhập phải ở trạng thái Approved hoặc Receiving mới có thể bắt đầu nhận hàng.", ApiCodeConstants.Common.UnprocessableEntity);

        var item = await _inboundOrderItemRepository.FirstOrDefaultAsync(x => x.Id == dto.InboundOrderItemId && !x.IsDeleted && x.InboundOrderId == orderId, true);
        if (item == null)
            return ApiResponse.NotFound("Không tìm thấy sản phẩm cần nhận trong phiếu nhập.", ApiCodeConstants.Common.NotFound);

        if (item.ProductVariantId.HasValue)
        {
            var pv = await _productVariantRepository.FirstOrDefaultAsync(x => x.Id == item.ProductVariantId.Value && !x.IsDeleted && x.IsActive);
            if (pv == null)
                return ApiResponse.UnprocessableEntity("Sản phẩm đã bị khóa hoặc xóa khỏi hệ thống.", ApiCodeConstants.Common.UnprocessableEntity);
        }

        if (!order.Warehouse.IsActive || order.Warehouse.IsDeleted)
            return ApiResponse.UnprocessableEntity("Kho hàng hiện đang không hoạt động.", ApiCodeConstants.Common.UnprocessableEntity);

        // Initialize state if not already receiving or if cancelled
        var state = item.GetReceiptState();
        if (state.ReceiptStatus == "Confirmed")
        {
            return ApiResponse.UnprocessableEntity("Sản phẩm này đã được xác nhận nhập kho hoàn tất.", ApiCodeConstants.Common.UnprocessableEntity);
        }

        state.ReceiptStatus = "Draft";
        state.OriginalNote = state.OriginalNote ?? item.Note;
        item.SaveReceiptState(state);

        await _inboundOrderItemRepository.UpdateAsync(item);

        // If document is Approved, move to Receiving
        if (order.InboundOrderStatus.Name == InboundOrderStatusNames.Approved)
        {
            order.InboundOrderStatusId = await GetStatusIdAsync(InboundOrderStatusNames.Receiving);
            await _inboundOrderRepository.UpdateAsync(order);
        }

        await _inboundOrderRepository.SaveChangesAsync();

        return ApiResponse.Success(item.ToDto(), "Bắt đầu nhận hàng thành công.");
    }

    public async Task<ApiResponse> ScanQrAsync(int orderId, int receiptId, ScanQrDto dto)
    {
        var order = await _inboundOrderRepository.FirstOrDefaultAsync(x => x.Id == orderId && !x.IsDeleted, false);
        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

        var item = await _inboundOrderItemRepository.FirstOrDefaultAsync(x => x.Id == receiptId && !x.IsDeleted && x.InboundOrderId == orderId, true, x => x.ProductVariant);
        if (item == null)
            return ApiResponse.NotFound("Không tìm thấy receipt/item tương ứng.", ApiCodeConstants.Common.NotFound);

        var state = item.GetReceiptState();
        if (state.ReceiptStatus == "Confirmed" || state.ReceiptStatus == "Cancelled")
            return ApiResponse.UnprocessableEntity("Trạng thái receipt không hợp lệ để quét mã QR.", ApiCodeConstants.Common.UnprocessableEntity);

        if (string.IsNullOrWhiteSpace(dto.QrCode))
            return ApiResponse.BadRequest("Mã QR không được để trống.", ApiCodeConstants.Common.BadRequest);

        var variant = await _productVariantRepository.FirstOrDefaultAsync(x => x.QRCode == dto.QrCode && !x.IsDeleted && x.IsActive, false, x => x.Product);
        if (variant == null)
            return ApiResponse.NotFound("Không tìm thấy sản phẩm có mã QR tương ứng.", ApiCodeConstants.Common.NotFound);

        if (item.ProductVariantId != variant.Id)
            return ApiResponse.UnprocessableEntity("Mã QR quét được không khớp với sản phẩm được khai báo trong phiếu.", ApiCodeConstants.Common.UnprocessableEntity);

        item.QRScanned = true;
        await _inboundOrderItemRepository.UpdateAsync(item);
        await _inboundOrderItemRepository.SaveChangesAsync();

        var pvDto = variant.ToDto();
        return ApiResponse.Success(pvDto, "Quét mã QR sản phẩm thành công.");
    }

    public async Task<ApiResponse> RecordQuantityAsync(int orderId, int receiptId, RecordQuantityDto dto)
    {
        var order = await _inboundOrderRepository.FirstOrDefaultAsync(x => x.Id == orderId && !x.IsDeleted, false);
        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

        var item = await _inboundOrderItemRepository.FirstOrDefaultAsync(x => x.Id == receiptId && !x.IsDeleted && x.InboundOrderId == orderId, true, x => x.ProductVariant);
        if (item == null)
            return ApiResponse.NotFound("Không tìm thấy receipt/item tương ứng.", ApiCodeConstants.Common.NotFound);

        var state = item.GetReceiptState();
        if (state.ReceiptStatus == "Confirmed" || state.ReceiptStatus == "Cancelled")
            return ApiResponse.UnprocessableEntity("Trạng thái receipt không hợp lệ để ghi nhận số lượng.", ApiCodeConstants.Common.UnprocessableEntity);

        if (dto.QuantityReceived <= 0)
            return ApiResponse.UnprocessableEntity("Số lượng nhận phải lớn hơn 0.", ApiCodeConstants.Common.UnprocessableEntity);

        // Confirmed quantity currently is item.QuantityReceived (committed quantity)
        var confirmedQty = item.QuantityReceived;
        var remainingExpected = item.QuantityOrdered - confirmedQty;

        state.QuantityEntered = dto.QuantityReceived;
        state.OriginalNote = dto.Note ?? state.OriginalNote;

        if (dto.QuantityReceived > remainingExpected)
        {
            if (string.IsNullOrWhiteSpace(dto.Note))
            {
                return ApiResponse.UnprocessableEntity("Yêu cầu nhập lý do nhận vượt số lượng đặt.", ApiCodeConstants.Common.UnprocessableEntity);
            }
            state.OverReceiveReason = dto.Note;
            state.ReceiptStatus = "PendingManagerReview";
            item.SaveReceiptState(state);
            await _inboundOrderItemRepository.UpdateAsync(item);
            await _inboundOrderItemRepository.SaveChangesAsync();

            return ApiResponse.Success(item.ToDto(), "Nhận hàng vượt số lượng dự kiến. Chờ quản lý phê duyệt.");
        }
        else
        {
            state.OverReceiveReason = null;
            state.ReceiptStatus = "QuantityEntered";
            // Set expected weight snapshots
            if (item.ProductVariant != null)
            {
                item.ExpectedWeightKg = item.ProductVariant.Weight * dto.QuantityReceived;
            }

            item.SaveReceiptState(state);
            await _inboundOrderItemRepository.UpdateAsync(item);
            await _inboundOrderItemRepository.SaveChangesAsync();

            return ApiResponse.Success(item.ToDto(), "Ghi nhận số lượng nhận thành công.");
        }
    }

    public async Task<ApiResponse> AttachWeightAsync(int orderId, int receiptId, AttachWeightDto dto)
    {
        var order = await _inboundOrderRepository.FirstOrDefaultAsync(x => x.Id == orderId && !x.IsDeleted, false);
        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

        var item = await _inboundOrderItemRepository.FirstOrDefaultAsync(x => x.Id == receiptId && !x.IsDeleted && x.InboundOrderId == orderId, true, x => x.ProductVariant);
        if (item == null)
            return ApiResponse.NotFound("Không tìm thấy receipt/item tương ứng.", ApiCodeConstants.Common.NotFound);

        var state = item.GetReceiptState();
        if (state.ReceiptStatus == "Confirmed" || state.ReceiptStatus == "Cancelled")
            return ApiResponse.UnprocessableEntity("Trạng thái receipt không hợp lệ để gắn cân nặng.", ApiCodeConstants.Common.UnprocessableEntity);

        if (item.ProductVariant == null)
            return ApiResponse.UnprocessableEntity("Sản phẩm không hợp lệ.", ApiCodeConstants.Common.UnprocessableEntity);

        if (!state.QuantityEntered.HasValue || state.QuantityEntered.Value <= 0)
            return ApiResponse.UnprocessableEntity("Vui lòng ghi nhận số lượng nhận trước khi gắn cân nặng.", ApiCodeConstants.Common.UnprocessableEntity);

        if (state.ReceiptStatus == "PendingManagerReview" && !string.IsNullOrEmpty(state.OverReceiveReason) && state.ExceptionDecision != "Approve")
            return ApiResponse.UnprocessableEntity("Đang chờ quản lý duyệt ngoại lệ nhận quá số lượng.", ApiCodeConstants.Common.UnprocessableEntity);

        // Validation for IoT Log
        var log = await _iotWeightLogRepository.FirstOrDefaultAsync(x => x.Id == dto.IotWeightLogId && !x.IsDeleted, true, x => x.IotDevice);
        if (log == null)
            return ApiResponse.NotFound("Không tìm thấy log cân nặng thiết bị IoT.", ApiCodeConstants.Common.NotFound);

        if (!log.IsStable)
            return ApiResponse.UnprocessableEntity("Khối lượng chưa ổn định.", ApiCodeConstants.Common.UnprocessableEntity);

        // Cho phép gắn trực tiếp từ log cân ổn định (không bắt buộc gọi attach-context trước).
        // Chỉ chặn khi log đã gắn cho một dòng hàng KHÁC để tránh tái sử dụng bằng chứng cân.
        if (log.IsConfirmed && log.ReferenceItemId.HasValue && log.ReferenceItemId.Value != item.Id)
            return ApiResponse.UnprocessableEntity("Log cân này đã được gắn cho một dòng hàng khác.", ApiCodeConstants.Common.UnprocessableEntity);

        if (log.WeightKg <= 0)
            return ApiResponse.UnprocessableEntity("Log cân phải có khối lượng lớn hơn 0.", ApiCodeConstants.Common.UnprocessableEntity);

        if (!log.IotDevice.IsActive || log.IotDevice.IsDeleted || !log.IotDevice.IsOnline)
            return ApiResponse.UnprocessableEntity("Thiết bị IoT đang không hoạt động hoặc không trực tuyến.", ApiCodeConstants.Common.UnprocessableEntity);

        if (log.IotDevice.WarehouseId != order.WarehouseId)
            return ApiResponse.UnprocessableEntity("Thiết bị cân không thuộc kho hàng của phiếu nhập này.", ApiCodeConstants.Common.UnprocessableEntity);

        if (log.ProductVariantId != item.ProductVariantId)
            return ApiResponse.UnprocessableEntity("Sản phẩm cân không khớp với sản phẩm trong phiếu nhập.", ApiCodeConstants.Common.UnprocessableEntity);

        // Resolve dung sai theo thứ tự ưu tiên: ProductCategory -> Warehouse -> global -> mặc định (BR-19/BR-20).
        // Khóa chuẩn theo tài liệu là IOT_WEIGHT_TOLERANCE_PERCENT; vẫn fallback khóa cũ WeightTolerancePercent.
        int? productCategoryId = null;
        var variantWithProduct = await _productVariantRepository.FirstOrDefaultAsync(
            x => x.Id == item.ProductVariantId, false, x => x.Product);
        if (variantWithProduct?.Product != null)
            productCategoryId = variantWithProduct.Product.ProductCategoryId;

        var tolerancePercent = await ResolveConfigDecimalAsync(
            5m,
            $"IOT_WEIGHT_TOLERANCE_PERCENT:CATEGORY:{productCategoryId}",
            $"IOT_WEIGHT_TOLERANCE_PERCENT:WAREHOUSE:{order.WarehouseId}",
            "IOT_WEIGHT_TOLERANCE_PERCENT",
            $"WeightTolerancePercent:{order.WarehouseId}",
            "WeightTolerancePercent");

        var minToleranceKg = await ResolveConfigDecimalAsync(
            0.05m,
            $"IOT_WEIGHT_MIN_TOLERANCE_KG:CATEGORY:{productCategoryId}",
            $"IOT_WEIGHT_MIN_TOLERANCE_KG:WAREHOUSE:{order.WarehouseId}",
            "IOT_WEIGHT_MIN_TOLERANCE_KG",
            $"WeightMinimumToleranceKg:{order.WarehouseId}",
            "WeightMinimumToleranceKg");

        var qty = state.QuantityEntered.Value;
        var expectedWeight = item.ProductVariant.Weight * qty;
        var allowedDiff = Math.Max(expectedWeight * (tolerancePercent / 100m), minToleranceKg);
        var actualWeight = log.WeightKg;
        var diff = Math.Abs(actualWeight - expectedWeight);

        state.IotWeightLogId = dto.IotWeightLogId;
        item.ExpectedWeightKg = expectedWeight;
        item.ActualWeightKg = actualWeight;

        // Gắn & xác nhận bằng chứng cân vào dòng hàng (gộp luồng, thay cho việc gọi attach-context riêng).
        // Bằng chứng được liên kết qua ReferenceType/ReferenceId/ReferenceItemId dù sai lệch hay không (BR-18).
        var weightConfirmedBy = GetCurrentUserId();
        var weightConfirmedAt = DateTime.Now;
        log.ProductVariantId = item.ProductVariantId;
        log.ReferenceType = "INBOUND_ORDER";
        log.ReferenceId = order.Id;
        log.ReferenceItemId = item.Id;
        log.IsConfirmed = true;
        log.ConfirmedBy = weightConfirmedBy;
        log.ConfirmedAt = weightConfirmedAt;
        log.LastModifiedDate = weightConfirmedAt;
        await _iotWeightLogRepository.UpdateAsync(log);

        if (diff > allowedDiff)
        {
            state.WeightDiscrepancyReason = $"Sai lệch cân nặng ({diff} kg) vượt mức cho phép ({allowedDiff} kg).";
            state.ReceiptStatus = "PendingManagerReview";
            item.SaveReceiptState(state);
            await _inboundOrderItemRepository.UpdateAsync(item);
            await _inboundOrderItemRepository.SaveChangesAsync();

            return ApiResponse.UnprocessableEntity("Sai lệch khối lượng vượt mức cho phép. Chờ quản lý phê duyệt ngoại lệ.", ApiCodeConstants.Common.UnprocessableEntity);
        }
        else
        {
            state.WeightDiscrepancyReason = null;
            state.ReceiptStatus = "WeightVerified";
            item.SaveReceiptState(state);
            await _inboundOrderItemRepository.UpdateAsync(item);
            await _inboundOrderItemRepository.SaveChangesAsync();

            return ApiResponse.Success(item.ToDto(), "Xác thực cân nặng thành công.");
        }
    }

    /// <summary>
    /// Lấy giá trị decimal từ SystemConfig theo danh sách khóa ưu tiên; trả về defaultValue nếu không khóa nào hợp lệ.
    /// </summary>
    private async Task<decimal> ResolveConfigDecimalAsync(decimal defaultValue, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var raw = await _systemConfigRepository.GetValueByKey(key);
            if (!string.IsNullOrWhiteSpace(raw) && decimal.TryParse(raw, out var value))
                return value;
        }

        return defaultValue;
    }

    public async Task<ApiResponse> ReviewExceptionAsync(int orderId, int receiptId, ReviewExceptionDto dto)
    {
        var order = await _inboundOrderRepository.FirstOrDefaultAsync(x => x.Id == orderId && !x.IsDeleted, false);
        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

        var item = await _inboundOrderItemRepository.FirstOrDefaultAsync(x => x.Id == receiptId && !x.IsDeleted && x.InboundOrderId == orderId, true);
        if (item == null)
            return ApiResponse.NotFound("Không tìm thấy receipt/item tương ứng.", ApiCodeConstants.Common.NotFound);

        var state = item.GetReceiptState();
        if (state.ReceiptStatus != "PendingManagerReview")
            return ApiResponse.UnprocessableEntity("Receipt không ở trạng thái cần phê duyệt ngoại lệ.", ApiCodeConstants.Common.UnprocessableEntity);

        state.ExceptionDecision = dto.Decision;
        state.ExceptionReason = dto.Reason;

        if (dto.Decision == "Approve")
        {
            // Resolve correct correctable state
            // If it was over-receiving, it can go to QuantityEntered so they proceed to weight or select putaway
            // If it was weight discrepancy, it goes to WeightVerified
            if (!string.IsNullOrEmpty(state.WeightDiscrepancyReason))
            {
                state.ReceiptStatus = "WeightVerified";
            }
            else if (!string.IsNullOrEmpty(state.OverReceiveReason))
            {
                state.ReceiptStatus = "QuantityEntered";
            }
            else
            {
                state.ReceiptStatus = "WeightVerified"; // Fallback
            }
        }
        else // Reject
        {
            state.ReceiptStatus = "Cancelled";
        }

        item.SaveReceiptState(state);
        await _inboundOrderItemRepository.UpdateAsync(item);
        await _inboundOrderItemRepository.SaveChangesAsync();

        await LogAuditAsync("APPROVE_REJECT_EXCEPTION", nameof(InboundOrderItem), item.Id.ToString(), $"Quản lý quyết định {dto.Decision} cho ngoại lệ của receipt {item.Id}. Lý do: {dto.Reason}");

        return ApiResponse.Success(item.ToDto(), $"Quyết định phê duyệt ngoại lệ: {dto.Decision}.");
    }

    public async Task<ApiResponse> GetPutawaySuggestionsAsync(int orderId, int receiptId)
    {
        var order = await _inboundOrderRepository.FirstOrDefaultAsync(x => x.Id == orderId && !x.IsDeleted, false);
        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

        var item = await _inboundOrderItemRepository.FirstOrDefaultAsync(
            x => x.Id == receiptId && !x.IsDeleted && x.InboundOrderId == orderId,
            false,
            x => x.ProductVariant,
            x => x.ProductVariant.Product
        );
        if (item == null)
            return ApiResponse.NotFound("Không tìm thấy receipt/item tương ứng.", ApiCodeConstants.Common.NotFound);

        var state = item.GetReceiptState();
        if (state.ReceiptStatus == "Confirmed" || state.ReceiptStatus == "Cancelled")
            return ApiResponse.UnprocessableEntity("Receipt đã hoàn tất hoặc đã bị hủy.", ApiCodeConstants.Common.UnprocessableEntity);

        if (!state.QuantityEntered.HasValue || state.QuantityEntered.Value <= 0)
            return ApiResponse.UnprocessableEntity("Vui lòng ghi nhận số lượng trước khi đề xuất vị trí cất hàng.", ApiCodeConstants.Common.UnprocessableEntity);

        if (item.ProductVariant != null && item.ProductVariant.IsIoTRequired && state.ReceiptStatus != "WeightVerified" && state.ReceiptStatus != "PutawaySelected")
        {
            return ApiResponse.UnprocessableEntity("Sản phẩm yêu cầu xác thực khối lượng IoT trước.", ApiCodeConstants.Common.UnprocessableEntity);
        }

        // Putaway suggestions scoring configuration
        double catMatchW = 0.40;
        double capFitW = 0.30;
        double occW = 0.20;
        double priW = 0.10;

        var whCatMatchStr = await _systemConfigRepository.GetValueByKey($"PutawayCategoryMatchWeight:{order.WarehouseId}");
        var whCapFitStr = await _systemConfigRepository.GetValueByKey($"PutawayCapacityFitWeight:{order.WarehouseId}");
        var whOccStr = await _systemConfigRepository.GetValueByKey($"PutawayOccupancyWeight:{order.WarehouseId}");
        var whPriStr = await _systemConfigRepository.GetValueByKey($"PutawayPriorityWeight:{order.WarehouseId}");

        if (string.IsNullOrEmpty(whCatMatchStr)) whCatMatchStr = await _systemConfigRepository.GetValueByKey("PutawayCategoryMatchWeight");
        if (string.IsNullOrEmpty(whCapFitStr)) whCapFitStr = await _systemConfigRepository.GetValueByKey("PutawayCapacityFitWeight");
        if (string.IsNullOrEmpty(whOccStr)) whOccStr = await _systemConfigRepository.GetValueByKey("PutawayOccupancyWeight");
        if (string.IsNullOrEmpty(whPriStr)) whPriStr = await _systemConfigRepository.GetValueByKey("PutawayPriorityWeight");

        if (double.TryParse(whCatMatchStr, out var w1)) catMatchW = w1;
        if (double.TryParse(whCapFitStr, out var w2)) capFitW = w2;
        if (double.TryParse(whOccStr, out var w3)) occW = w3;
        if (double.TryParse(whPriStr, out var w4)) priW = w4;

        if (catMatchW < 0 || capFitW < 0 || occW < 0 || priW < 0 || Math.Abs((catMatchW + capFitW + occW + priW) - 1.0) > 0.001)
        {
            return ApiResponse.UnprocessableEntity("Cấu hình trọng số Smart Put-away không hợp lệ (Trọng số phải không âm và có tổng bằng 1.0).", ApiCodeConstants.Common.UnprocessableEntity);
        }

        var receiptQty = state.QuantityEntered.Value;
        var pcatId = item.ProductVariant?.Product?.ProductCategoryId;

        // Fetch candidate locations
        var candidates = await _locationRepository.FindByConditionAsync(x =>
            x.WarehouseId == order.WarehouseId &&
            x.IsActive &&
            !x.IsDeleted &&
            !x.IsQuarantine &&
            x.MaxCapacity.HasValue && x.MaxCapacity.Value > 0 &&
            (x.CurrentOccupancy + receiptQty) <= x.MaxCapacity.Value &&
            (x.AllowedCategoryId == null || (pcatId.HasValue && x.AllowedCategoryId.Value == pcatId.Value))
        );

        if (!candidates.Any())
        {
            return ApiResponse.Success(new List<PutawaySuggestionDto>(), "Không tìm thấy vị trí cất hàng phù hợp đủ sức chứa.");
        }

        var maxPriority = candidates.Max(x => x.Priority);
        var suggestions = new List<PutawaySuggestionDto>();

        foreach (var loc in candidates)
        {
            var availCap = loc.MaxCapacity!.Value - loc.CurrentOccupancy;

            // Category match score
            double catScore = 0.60;
            if (loc.AllowedCategoryId.HasValue && pcatId.HasValue && loc.AllowedCategoryId.Value == pcatId.Value)
            {
                catScore = 1.00;
            }

            // Capacity fit score
            double fitScore = 1.0 - ((double)(availCap - receiptQty) / loc.MaxCapacity.Value);

            // Low occupancy score
            double occScore = 1.0 - ((double)loc.CurrentOccupancy / loc.MaxCapacity.Value);

            // Priority score
            double pScore = maxPriority > 0 ? (double)loc.Priority / maxPriority : 0.0;

            var finalScore = (catMatchW * catScore) + (capFitW * fitScore) + (occW * occScore) + (priW * pScore);

            suggestions.Add(new PutawaySuggestionDto
            {
                LocationId = loc.Id,
                ZoneName = loc.ZoneName,
                ShelfRow = loc.ShelfRow,
                ShelfLevel = loc.ShelfLevel,
                SlotCode = loc.SlotCode,
                Score = Math.Round(finalScore, 4),
                AvailableCapacity = availCap,
                CurrentOccupancy = loc.CurrentOccupancy,
                Priority = loc.Priority,
                CategoryMatch = loc.AllowedCategoryId.HasValue,
                ScoreBreakdown = new Dictionary<string, double>
                {
                    { "CategoryMatch", Math.Round(catMatchW * catScore, 4) },
                    { "CapacityFit", Math.Round(capFitW * fitScore, 4) },
                    { "Occupancy", Math.Round(occW * occScore, 4) },
                    { "Priority", Math.Round(priW * pScore, 4) }
                }
            });
        }

        var sorted = suggestions
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Priority)
            .ThenBy(x => x.AvailableCapacity)
            .ThenBy(x => x.LocationId)
            .ToList();

        return ApiResponse.Success(sorted);
    }

    public async Task<ApiResponse> SelectPutawayAsync(int orderId, int receiptId, SelectPutawayDto dto)
    {
        var order = await _inboundOrderRepository.FirstOrDefaultAsync(x => x.Id == orderId && !x.IsDeleted, false);
        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

        var item = await _inboundOrderItemRepository.FirstOrDefaultAsync(
            x => x.Id == receiptId && !x.IsDeleted && x.InboundOrderId == orderId,
            true,
            x => x.ProductVariant,
            x => x.ProductVariant.Product
        );
        if (item == null)
            return ApiResponse.NotFound("Không tìm thấy receipt/item tương ứng.", ApiCodeConstants.Common.NotFound);

        var state = item.GetReceiptState();
        var allowedStatus = new[] { "WeightVerified", "QuantityEntered", "PutawaySelected" };
        if (!allowedStatus.Contains(state.ReceiptStatus))
            return ApiResponse.UnprocessableEntity("Receipt phải ở trạng thái WeightVerified hoặc đã nhập số lượng hợp lệ.", ApiCodeConstants.Common.UnprocessableEntity);

        if (item.ProductVariant != null && item.ProductVariant.IsIoTRequired && state.ReceiptStatus == "QuantityEntered")
        {
            return ApiResponse.UnprocessableEntity("Sản phẩm này cần xác thực khối lượng IoT trước.", ApiCodeConstants.Common.UnprocessableEntity);
        }

        var loc = await _locationRepository.FirstOrDefaultAsync(x => x.Id == dto.LocationId && !x.IsDeleted && x.IsActive);
        if (loc == null)
            return ApiResponse.NotFound("Vị trí lưu trữ không tồn tại hoặc đã bị khóa.", ApiCodeConstants.Common.NotFound);

        if (loc.IsQuarantine)
            return ApiResponse.UnprocessableEntity("Không cho phép chọn vị trí cách ly (Quarantine) để cất hàng nhập thông thường.", ApiCodeConstants.Common.UnprocessableEntity);

        if (loc.WarehouseId != order.WarehouseId)
            return ApiResponse.UnprocessableEntity("Vị trí lưu trữ không thuộc kho hàng của phiếu nhập này.", ApiCodeConstants.Common.UnprocessableEntity);

        var qty = state.QuantityEntered ?? 0;
        if (loc.MaxCapacity.HasValue && (loc.CurrentOccupancy + qty) > loc.MaxCapacity.Value)
            return ApiResponse.UnprocessableEntity("Vị trí lưu trữ đã vượt quá sức chứa tối đa.", ApiCodeConstants.Common.UnprocessableEntity);

        var pcatId = item.ProductVariant?.Product?.ProductCategoryId;
        if (loc.AllowedCategoryId.HasValue && pcatId.HasValue && loc.AllowedCategoryId.Value != pcatId.Value)
            return ApiResponse.UnprocessableEntity("Sản phẩm không thuộc nhóm danh mục được phép cất giữ tại vị trí này.", ApiCodeConstants.Common.UnprocessableEntity);

        // Check if override suggestion
        var suggestionsResult = await GetPutawaySuggestionsAsync(orderId, receiptId);
        var isSuggested = false;
        if (suggestionsResult.Status == 200 && suggestionsResult.Resources is List<PutawaySuggestionDto> list)
        {
            isSuggested = list.Any(x => x.LocationId == dto.LocationId);
        }

        if (!isSuggested && !dto.IsOverride)
        {
            return ApiResponse.UnprocessableEntity("Vị trí đã chọn không nằm trong danh sách đề xuất. Vui lòng xác nhận Override.", ApiCodeConstants.Common.UnprocessableEntity);
        }

        if (dto.IsOverride && string.IsNullOrWhiteSpace(dto.OverrideReason))
        {
            return ApiResponse.UnprocessableEntity("Vui lòng nhập lý do chọn vị trí ngoài danh sách đề xuất.", ApiCodeConstants.Common.UnprocessableEntity);
        }

        state.ConfirmedLocationId = dto.LocationId;
        state.ConfirmedLocationCode = $"{loc.ZoneName}-{loc.ShelfRow}-{loc.ShelfLevel}-{loc.SlotCode}";
        state.PutawayOverrideReason = dto.IsOverride ? dto.OverrideReason : null;
        state.ReceiptStatus = "PutawaySelected";

        item.SaveReceiptState(state);
        await _inboundOrderItemRepository.UpdateAsync(item);
        await _inboundOrderItemRepository.SaveChangesAsync();

        return ApiResponse.Success(item.ToDto(), "Chọn vị trí cất hàng thành công.");
    }

    public async Task<ApiResponse> ConfirmReceiptAsync(int orderId, int receiptId, ConfirmReceiptDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.OperationKey))
            return ApiResponse.BadRequest("Thiếu OperationKey (Idempotency Key).", ApiCodeConstants.Common.BadRequest);

        await using var transaction = await _inboundOrderRepository.BeginTransactionAsync();

        try
        {
            var order = await _inboundOrderRepository.FirstOrDefaultAsync(
                x => x.Id == orderId && !x.IsDeleted,
                true,
                x => x.InboundOrderItems
            );
            if (order == null)
                return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

            var item = await _inboundOrderItemRepository.FirstOrDefaultAsync(
                x => x.Id == receiptId && !x.IsDeleted && x.InboundOrderId == orderId,
                true,
                x => x.ProductVariant
            );
            if (item == null)
                return ApiResponse.NotFound("Không tìm thấy receipt/item tương ứng.", ApiCodeConstants.Common.NotFound);

            var state = item.GetReceiptState();

            // Idempotency check: if already Confirmed
            if (state.ReceiptStatus == "Confirmed")
            {
                // Re-return success response
                await transaction.RollbackAsync();
                return ApiResponse.Success(item.ToDto(), "Phiếu đã được xác nhận nhập kho thành công trước đó (Idempotent).");
            }

            if (state.ReceiptStatus != "PutawaySelected" || !state.ConfirmedLocationId.HasValue)
                return ApiResponse.UnprocessableEntity("Receipt phải ở trạng thái PutawaySelected để xác nhận nhập kho.", ApiCodeConstants.Common.UnprocessableEntity);

            var loc = await _locationRepository.FirstOrDefaultAsync(x => x.Id == state.ConfirmedLocationId.Value && !x.IsDeleted && x.IsActive, true);
            if (loc == null || loc.IsQuarantine)
                return ApiResponse.UnprocessableEntity("Vị trí lưu trữ không còn khả dụng hoặc đã bị đưa vào khu cách ly.", ApiCodeConstants.Common.UnprocessableEntity);

            var qty = state.QuantityEntered ?? 0;
            if (loc.MaxCapacity.HasValue && (loc.CurrentOccupancy + qty) > loc.MaxCapacity.Value)
                return ApiResponse.UnprocessableEntity("Vị trí lưu trữ hiện đã hết sức chứa.", ApiCodeConstants.Common.UnprocessableEntity);

            // Recheck RowVersion/concurrency: fetch inventory with tracking
            var inventory = await _inventoryRepository.FirstOrDefaultAsync(x =>
                x.WarehouseId == order.WarehouseId &&
                x.LocationId == loc.Id &&
                x.ProductVariantId == item.ProductVariantId,
                true
            );

            var oldQty = 0;
            decimal oldCost = 0;

            if (inventory == null)
            {
                inventory = new Inventory
                {
                    WarehouseId = order.WarehouseId,
                    LocationId = loc.Id,
                    ProductVariantId = item.ProductVariantId!.Value,
                    QuantityOnHand = 0,
                    QuantityReserved = 0,
                    CostPrice = item.UnitCostPrice,
                    CreatedDate = DateTime.Now,
                    CreatedBy = GetCurrentUserId()
                };
                await _inventoryRepository.CreateAsync(inventory);
                // We save to generate inventory Id so transaction mapping works
                await _inventoryRepository.SaveChangesAsync();
            }
            else
            {
                oldQty = inventory.QuantityOnHand;
                oldCost = inventory.CostPrice;
            }

            // Weighted average cost price calculation
            if (inventory.QuantityOnHand > 0 && inventory.CostPrice > 0)
            {
                var newCost = ((oldQty * oldCost) + (qty * item.UnitCostPrice)) / (oldQty + qty);
                inventory.CostPrice = Math.Round(newCost, 2);
            }
            else
            {
                inventory.CostPrice = item.UnitCostPrice;
            }

            inventory.QuantityOnHand += qty;
            inventory.LastModifiedDate = DateTime.Now;
            inventory.UpdatedBy = GetCurrentUserId();

            await _inventoryRepository.UpdateAsync(inventory);

            // Increase Location occupancy
            loc.CurrentOccupancy += qty;
            await _locationRepository.UpdateAsync(loc);

            // Update item quantity received and weights
            item.QuantityReceived += qty;
            state.ReceiptStatus = "Confirmed";
            item.SaveReceiptState(state);
            await _inboundOrderItemRepository.UpdateAsync(item);

            // Create Inventory Transaction record
            var invTrans = new InventoryTransaction
            {
                InventoryId = inventory.Id,
                WarehouseId = order.WarehouseId,
                LocationId = loc.Id,
                ProductVariantId = item.ProductVariantId,
                TransactionType = "INBOUND_RECEIVE",
                ReferenceType = "InboundReceipt",
                ReferenceId = item.Id,
                ReferenceItemId = item.Id,
                Quantity = qty,
                BeforeQuantity = oldQty,
                AfterQuantity = inventory.QuantityOnHand,
                WeightKg = item.ActualWeightKg,
                IotWeightLogId = state.IotWeightLogId,
                Note = state.OriginalNote,
                CreatedBy = GetCurrentUserId(),
                CreatedDate = DateTime.Now
            };

            await _inventoryTransactionRepository.CreateAsync(invTrans);

            // Create AuditLog entry
            var auditLog = new AuditLog
            {
                Action = "CONFIRM_RECEIVE",
                TargetType = nameof(InboundOrderItem),
                TargetId = item.Id.ToString(),
                Description = $"Xác nhận nhập kho hoàn tất dòng hàng {item.Id} của phiếu {order.POCode}. Nhập {qty} sản phẩm vào vị trí {state.ConfirmedLocationCode}.",
                CreatedBy = GetCurrentUserId(),
                CreatedDate = DateTime.Now,
                IpAddress = _httpContextAccessor.HttpContext?.GetRemoteHostIpAddress(),
                UserAgent = _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString()
            };
            await _auditLogRepository.CreateAsync(auditLog);

            await _inboundOrderRepository.SaveChangesAsync();

            // Recalculate document status
            // Reload all items to get accurate numbers
            var allItems = await _inboundOrderItemRepository.FindByConditionAsync(x => x.InboundOrderId == order.Id && !x.IsDeleted);
            var totalLines = allItems.Count;
            var settledLines = allItems.Count(x => x.QuantityReceived >= x.QuantityOrdered);
            var hasAnyReceived = allItems.Any(x => x.QuantityReceived > 0);

            var nextDocStatusName = InboundOrderStatusNames.Receiving;
            if (settledLines == totalLines)
            {
                nextDocStatusName = InboundOrderStatusNames.Confirmed;
                order.CompletedDate = DateTime.Now;
            }
            else if (hasAnyReceived)
            {
                nextDocStatusName = InboundOrderStatusNames.PartiallyReceived;
            }

            order.InboundOrderStatusId = await GetStatusIdAsync(nextDocStatusName);
            await _inboundOrderRepository.UpdateAsync(order);
            await _inboundOrderRepository.SaveChangesAsync();

            await transaction.CommitAsync();

            // Realtime giờ do AuditSaveChangesInterceptor tự phát khi dữ liệu đổi
            // (InboundOrder nằm trong RealtimeEntityNames), không cần publish thủ công.

            return ApiResponse.Success(item.ToDto(), "Xác nhận nhập kho hoàn tất thành công.");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Optimistic concurrency conflict occurred.");
            var response = ApiResponse.Error("Xung đột dữ liệu đồng thời. Vui lòng tải lại và thử lại.", 409, ApiCodeConstants.Common.DuplicatedData);
            return response;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to confirm receipt.");
            return ApiResponse.InternalServerError();
        }
    }

    public async Task<ApiResponse> GetReceiptsAsync(int orderId)
    {
        var items = await _inboundOrderItemRepository.FindByCondition(x => x.InboundOrderId == orderId && !x.IsDeleted, false, x => x.ProductVariant)
            .ToListAsync();

        var list = items.Select(x => x.ToDto()).ToList();
        return ApiResponse.Success(list);
    }
}
