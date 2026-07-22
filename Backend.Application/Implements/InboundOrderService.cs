using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Backend.Domain.DTParameters;
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
    private readonly ISystemConfigRepository _systemConfigRepository;
    private readonly IRepositoryBase<DeliveryNote, int> _deliveryNoteRepository;
    private readonly IRepositoryBase<FileUpload, int> _fileUploadRepository;
    private readonly IStorageService _storageService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<InboundOrderService> _logger;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly IRepositoryBase<PaddyPurchaseReceipt, int> _paddyPurchaseReceiptRepository;
    private readonly IRepositoryBase<PaddyPurchaseSchedule, int> _paddyPurchaseScheduleRepository;
    private readonly ISystemLookup _systemLookup;
    private readonly IRepositoryBase<PurchaseOrder, int> _purchaseOrderRepository;
    private readonly IRepositoryBase<PurchaseOrderStatus, int> _purchaseOrderStatusRepository;
    private readonly IRepositoryBase<PaddyLot, int> _paddyLotRepository;
    private readonly IRepositoryBase<LotStatus, int> _lotStatusRepository;
    private readonly IPaddyPurchaseReceiptService _paddyPurchaseReceiptService;

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
        ISystemConfigRepository systemConfigRepository,
        IRepositoryBase<DeliveryNote, int> deliveryNoteRepository,
        IRepositoryBase<FileUpload, int> fileUploadRepository,
        IStorageService storageService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<InboundOrderService> logger,
        INotificationDispatcher notificationDispatcher,
        IRepositoryBase<PaddyPurchaseReceipt, int> paddyPurchaseReceiptRepository,
        IRepositoryBase<PaddyPurchaseSchedule, int> paddyPurchaseScheduleRepository,
        ISystemLookup systemLookup,
        IRepositoryBase<PurchaseOrder, int> purchaseOrderRepository,
        IRepositoryBase<PurchaseOrderStatus, int> purchaseOrderStatusRepository,
        IRepositoryBase<PaddyLot, int> paddyLotRepository,
        IRepositoryBase<LotStatus, int> lotStatusRepository,
        IPaddyPurchaseReceiptService paddyPurchaseReceiptService)
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
        _systemConfigRepository = systemConfigRepository;
        _deliveryNoteRepository = deliveryNoteRepository;
        _fileUploadRepository = fileUploadRepository;
        _storageService = storageService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _notificationDispatcher = notificationDispatcher;
        _paddyPurchaseReceiptRepository = paddyPurchaseReceiptRepository;
        _paddyPurchaseScheduleRepository = paddyPurchaseScheduleRepository;
        _systemLookup = systemLookup;
        _purchaseOrderRepository       = purchaseOrderRepository;
        _purchaseOrderStatusRepository = purchaseOrderStatusRepository;
        _paddyLotRepository            = paddyLotRepository;
        _lotStatusRepository           = lotStatusRepository;
        _paddyPurchaseReceiptService   = paddyPurchaseReceiptService;
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

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _inboundOrderRepository
            .FindByCondition(x => !x.IsDeleted, false, x => x.Warehouse, x => x.Supplier, x => x.InboundOrderStatus);

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword.ToLower();
            data = data.Where(x => (x.POCode ?? "").ToLower().Contains(keyword) ||
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

    /// <summary>
    /// Phân trang nâng cao (DataTables) cho màn web quản lý phiếu nhập:
    /// tìm kiếm chung (POCode/NCC/ghi chú), lọc theo cột (trạng thái, khoảng ngày dự kiến/ngày tạo) và sắp xếp theo cột.
    /// </summary>
    public async Task<ApiResponse> GetPagedAdvancedAsync(InboundOrderDTParameters parameters)
    {
        var keyword = parameters.Search?.Value?.Trim();
        var orderCriteria = "CreatedDate";
        var orderAscendingDirection = false;

        if (parameters.Order != null && parameters.Order.Any() && parameters.Columns != null && parameters.Columns.Any())
        {
            var rawColumn = parameters.Columns[parameters.Order[0].Column].Data;
            orderCriteria = NormalizeInboundOrderColumn(rawColumn);
            orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() == "asc";
        }

        var query = _inboundOrderRepository
            .FindByCondition(x => !x.IsDeleted, false)
            .Select(x => new InboundOrderListDto
            {
                Id = x.Id,
                POCode = x.POCode,
                WarehouseId = x.WarehouseId,
                WarehouseName = x.Warehouse.Name,
                SupplierId = x.SupplierId,
                SupplierName = x.Supplier != null ? x.Supplier.Name : null,
                InboundOrderStatusId = x.InboundOrderStatusId,
                InboundOrderStatusName = x.InboundOrderStatus.Name,
                TotalAssetValue = x.TotalAssetValue,
                ExpectedDate = x.ExpectedDate,
                CompletedDate = x.CompletedDate,
                Note = x.Note,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        // Tìm kiếm chung
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                (x.POCode ?? "").Contains(keyword) ||
                (x.SupplierName != null && x.SupplierName.Contains(keyword)) ||
                (x.Note != null && x.Note.Contains(keyword)));
        }

        // Lọc theo từng cột
        if (parameters.Columns != null)
        {
            foreach (var column in parameters.Columns)
            {
                var search = column.Search?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(search)) continue;

                switch (column.Data)
                {
                    case "poCode":
                    case "POCode":
                        query = query.Where(x => (x.POCode ?? "").Contains(search));
                        break;
                    case "supplierName":
                    case "SupplierName":
                        query = query.Where(x => x.SupplierName != null && x.SupplierName.Contains(search));
                        break;
                    case "warehouseName":
                    case "WarehouseName":
                        query = query.Where(x => x.WarehouseName.Contains(search));
                        break;
                    case "inboundOrderStatusName":
                    case "InboundOrderStatusName":
                        // Frontend gửi Id trạng thái -> lọc theo Id; nếu là chuỗi thì lọc theo tên
                        if (int.TryParse(search, out var statusId))
                            query = query.Where(x => x.InboundOrderStatusId == statusId);
                        else
                            query = query.Where(x => x.InboundOrderStatusName.Contains(search));
                        break;
                    case "expectedDate":
                    case "ExpectedDate":
                        if (search.Contains(" - "))
                        {
                            var dates = search.Split(" - ");
                            var startDate = DateTime.ParseExact(dates[0], "dd/MM/yyyy", CultureInfo.InvariantCulture);
                            var endDate = DateTime.ParseExact(dates[1], "dd/MM/yyyy", CultureInfo.InvariantCulture).AddDays(1).AddSeconds(-1);
                            query = query.Where(x => x.ExpectedDate >= startDate && x.ExpectedDate <= endDate);
                        }
                        else if (DateTime.TryParseExact(search, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expDate))
                        {
                            query = query.Where(x => x.ExpectedDate.HasValue && x.ExpectedDate.Value.Date == expDate.Date);
                        }
                        break;
                    case "createdDate":
                    case "CreatedDate":
                        if (search.Contains(" - "))
                        {
                            var dates = search.Split(" - ");
                            var startDate = DateTime.ParseExact(dates[0], "dd/MM/yyyy", CultureInfo.InvariantCulture);
                            var endDate = DateTime.ParseExact(dates[1], "dd/MM/yyyy", CultureInfo.InvariantCulture).AddDays(1).AddSeconds(-1);
                            query = query.Where(x => x.CreatedDate >= startDate && x.CreatedDate <= endDate);
                        }
                        else if (DateTime.TryParseExact(search, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var createdDate))
                        {
                            query = query.Where(x => x.CreatedDate.Date == createdDate.Date);
                        }
                        break;
                }
            }
        }

        var filteredRecord = await query.CountAsync();

        query = orderAscendingDirection
            ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc)
            : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = await query
            .Skip(parameters.Start)
            .Take(parameters.Length)
            .ToListAsync();

        return ApiResponse.Success(new DTResult<InboundOrderListDto>
        {
            draw = parameters.Draw,
            data = data,
            recordsFiltered = filteredRecord,
            recordsTotal = totalRecord
        });
    }

    private static string NormalizeInboundOrderColumn(string? columnName)
    {
        return columnName switch
        {
            "poCode" => "POCode",
            "supplierName" => "SupplierName",
            "warehouseName" => "WarehouseName",
            "inboundOrderStatusName" => "InboundOrderStatusName",
            "totalAssetValue" => "TotalAssetValue",
            "expectedDate" => "ExpectedDate",
            "createdDate" => "CreatedDate",
            _ => "CreatedDate"
        };
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

        var detail = order.ToDetailDto();

        // Gắn chứng từ giao hàng (nếu phiếu đã có) kèm URL ảnh để hiển thị/xem lại
        if (order.DeliveryNoteId.HasValue)
        {
            var deliveryNote = await _deliveryNoteRepository.FirstOrDefaultAsync(
                x => x.Id == order.DeliveryNoteId.Value && !x.IsDeleted, false, x => x.OriginalImageFile);
            if (deliveryNote != null)
            {
                var imageUrl = deliveryNote.OriginalImageFile != null
                    ? _storageService.GetOriginalUrl(deliveryNote.OriginalImageFile.FileKey)
                    : null;
                detail.DeliveryNote = deliveryNote.ToDto(order.Id, imageUrl);
            }
        }

        return ApiResponse.Success(detail);
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

        // Thông báo + push FCM cho người tạo phiếu và các vai trò quản lý.
        await _notificationDispatcher.DispatchAsync(
            NotificationConstants.Code.InboundApproved,
            BuildInboundNotifyTarget(order.CreatedBy),
            new object[] { order.POCode },
            $"/admin/inbound-orders/{order.Id}",
            GetCurrentUserId());

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

        // Thông báo + push FCM cho người tạo phiếu và các vai trò quản lý.
        await _notificationDispatcher.DispatchAsync(
            NotificationConstants.Code.InboundRejected,
            BuildInboundNotifyTarget(order.CreatedBy),
            new object[] { order.POCode, reason },
            $"/admin/inbound-orders/{order.Id}",
            GetCurrentUserId());

        return ApiResponse.Success();
    }

    /// <summary>Người nhận thông báo phiếu nhập: người tạo + vai trò quản lý.</summary>
    private static NotificationTarget BuildInboundNotifyTarget(int? createdBy)
    {
        return new NotificationTarget
        {
            UserIds = createdBy.HasValue ? new List<int> { createdBy.Value } : new List<int>(),
            RoleIds = new List<int>
            {
                CommonConstants.Role.ADMIN,
                CommonConstants.Role.EXECUTIVE,
                CommonConstants.Role.DISPATCHER,
            },
        };
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

        // BLE: số cân thực tế do app đọc trực tiếp từ cân qua Bluetooth và gửi lên (dto.ActualWeightKg).
        // Backend KHÔNG còn lưu bằng chứng cân từ thiết bị IoT — số cân được chốt thẳng vào dòng hàng của phiếu (BR-18).
        if (dto.ActualWeightKg <= 0)
            return ApiResponse.UnprocessableEntity("Khối lượng cân phải lớn hơn 0.", ApiCodeConstants.Common.UnprocessableEntity);

        // Resolve dung sai theo thứ tự ưu tiên: ProductCategory -> Warehouse -> global -> mặc định (BR-19/BR-20).
        int? productCategoryId = null;
        var variantWithProduct = await _productVariantRepository.FirstOrDefaultAsync(
            x => x.Id == item.ProductVariantId, false, x => x.Product);
        if (variantWithProduct?.Product != null)
            productCategoryId = variantWithProduct.Product.ProductCategoryId;

        var tolerancePercent = await ResolveConfigDecimalAsync(
            5m,
            $"WEIGHT_TOLERANCE_PERCENT:CATEGORY:{productCategoryId}",
            $"WEIGHT_TOLERANCE_PERCENT:WAREHOUSE:{order.WarehouseId}",
            "WEIGHT_TOLERANCE_PERCENT",
            $"WeightTolerancePercent:{order.WarehouseId}",
            "WeightTolerancePercent");

        var minToleranceKg = await ResolveConfigDecimalAsync(
            0.05m,
            $"WEIGHT_MIN_TOLERANCE_KG:CATEGORY:{productCategoryId}",
            $"WEIGHT_MIN_TOLERANCE_KG:WAREHOUSE:{order.WarehouseId}",
            "WEIGHT_MIN_TOLERANCE_KG",
            $"WeightMinimumToleranceKg:{order.WarehouseId}",
            "WeightMinimumToleranceKg");

        var qty = state.QuantityEntered.Value;
        var expectedWeight = item.ProductVariant.Weight * qty;
        var allowedDiff = Math.Max(expectedWeight * (tolerancePercent / 100m), minToleranceKg);
        var actualWeight = dto.ActualWeightKg;
        var diff = Math.Abs(actualWeight - expectedWeight);

        item.ExpectedWeightKg = expectedWeight;
        item.ActualWeightKg = actualWeight;

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
            double fitScore = 1.0 - ((double)(availCap - (decimal)receiptQty) / (double)loc.MaxCapacity.Value);

            // Low occupancy score
            double occScore = 1.0 - ((double)loc.CurrentOccupancy / (double)loc.MaxCapacity.Value);

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
                AvailableCapacity = (int)availCap,
                CurrentOccupancy = (int)loc.CurrentOccupancy,
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
            // RC-3 fix: phải bao gồm PaddyLotId trong điều kiện tìm — khớp unique index (variant, warehouse, location, lot)
            var paddyLotId = item.PaddyLotId; // nullable, null nếu không phải lô lúa
            var inventory = await _inventoryRepository.FirstOrDefaultAsync(x =>
                x.WarehouseId == order.WarehouseId &&
                x.LocationId == loc.Id &&
                x.ProductVariantId == item.ProductVariantId &&
                x.PaddyLotId == paddyLotId,
                true
            );

            var oldQty = 0m;
            decimal oldCost = 0;

            if (inventory == null)
            {
                inventory = new Inventory
                {
                    WarehouseId = order.WarehouseId,
                    LocationId = loc.Id,
                    ProductVariantId = item.ProductVariantId!.Value,
                    // RC-3 fix: gán PaddyLotId để tồn kho tách lô đúng theo unique index
                    PaddyLotId = paddyLotId,
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
                Note = state.OriginalNote,
                CreatedBy = GetCurrentUserId(),
                CreatedDate = DateTime.Now
            };

            await _inventoryTransactionRepository.CreateAsync(invTrans);

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
                order.CompletedDate = DateTimeHelper.VietnamNow();
            }
            else if (hasAnyReceived)
            {
                nextDocStatusName = InboundOrderStatusNames.PartiallyReceived;
            }

            order.InboundOrderStatusId = await GetStatusIdAsync(nextDocStatusName);
            await _inboundOrderRepository.UpdateAsync(order);
            // Cập nhật trạng thái lịch hẹn liên kết tự động bằng hàm dùng chung
            if (order.PaddyPurchaseReceiptId.HasValue)
            {
                var receipt = await _paddyPurchaseReceiptRepository.GetByIdAsync(order.PaddyPurchaseReceiptId.Value);
                if (receipt != null && receipt.ScheduleId.HasValue)
                {
                    await _paddyPurchaseReceiptService.UpdateScheduleStatusAsync(receipt.ScheduleId.Value, GetCurrentUserId());
                }
            }

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

    /// <summary>
    /// Lưu/gắn chứng từ giao hàng (ảnh + thông tin OCR) vào phiếu nhập.
    /// Ảnh phải được upload trước qua /file-manager/upload-by-category để lấy OriginalImageFileId.
    /// Quan hệ 1-1 do InboundOrder.DeliveryNoteId sở hữu: gọi lại sẽ cập nhật chứng từ hiện có.
    /// </summary>
    public async Task<ApiResponse> SaveDeliveryNoteAsync(int orderId, SaveDeliveryNoteDto dto)
    {
        var order = await _inboundOrderRepository.FirstOrDefaultAsync(x => x.Id == orderId && !x.IsDeleted, true);
        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

        // Xác thực ảnh chứng từ tồn tại trong kho tệp
        var file = await _fileUploadRepository.FirstOrDefaultAsync(x => x.Id == dto.OriginalImageFileId && !x.IsDeleted);
        if (file == null)
            return ApiResponse.UnprocessableEntity("Ảnh chứng từ không tồn tại hoặc đã bị xóa.", ApiCodeConstants.Common.UnprocessableEntity);

        var userId = GetCurrentUserId();
        var now = DateTime.Now;
        var isNew = !order.DeliveryNoteId.HasValue;

        DeliveryNote deliveryNote;
        if (!isNew)
        {
            deliveryNote = await _deliveryNoteRepository.FirstOrDefaultAsync(x => x.Id == order.DeliveryNoteId!.Value && !x.IsDeleted, true);
            if (deliveryNote == null)
            {
                isNew = true;
                deliveryNote = new DeliveryNote { CreatedBy = userId, CreatedDate = now };
            }
        }
        else
        {
            deliveryNote = new DeliveryNote { CreatedBy = userId, CreatedDate = now };
        }

        deliveryNote.TrackingCode = dto.TrackingCode;
        deliveryNote.CarrierName = dto.CarrierName;
        deliveryNote.SenderName = dto.SenderName;
        deliveryNote.SenderPhone = dto.SenderPhone;
        deliveryNote.SenderAddress = dto.SenderAddress;
        deliveryNote.ReceiverName = dto.ReceiverName;
        deliveryNote.ReceiverPhone = dto.ReceiverPhone;
        deliveryNote.ReceiverAddress = dto.ReceiverAddress;
        deliveryNote.DeclaredWeight = dto.DeclaredWeight;
        deliveryNote.CODAmount = dto.CODAmount;
        deliveryNote.RawOcrText = dto.RawOcrText;
        deliveryNote.OriginalImageFileId = dto.OriginalImageFileId;
        deliveryNote.IsConfirmed = dto.IsConfirmed;

        if (isNew)
        {
            await _deliveryNoteRepository.CreateAsync(deliveryNote);
            await _deliveryNoteRepository.SaveChangesAsync();

            order.DeliveryNoteId = deliveryNote.Id;
            order.UpdatedBy = userId;
            order.LastModifiedDate = now;
            await _inboundOrderRepository.UpdateAsync(order);
            await _inboundOrderRepository.SaveChangesAsync();
        }
        else
        {
            deliveryNote.UpdatedBy = userId;
            deliveryNote.LastModifiedDate = now;
            await _deliveryNoteRepository.UpdateAsync(deliveryNote);
            await _deliveryNoteRepository.SaveChangesAsync();
        }

        var imageUrl = _storageService.GetOriginalUrl(file.FileKey);
        return ApiResponse.Success(deliveryNote.ToDto(order.Id, imageUrl), "Lưu chứng từ giao hàng thành công.");
    }

    /// <summary>Lấy chứng từ giao hàng của phiếu nhập kèm URL ảnh.</summary>
    public async Task<ApiResponse> GetDeliveryNoteAsync(int orderId)
    {
        var order = await _inboundOrderRepository.FirstOrDefaultAsync(x => x.Id == orderId && !x.IsDeleted, false);
        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

        if (!order.DeliveryNoteId.HasValue)
            return ApiResponse.Success((DeliveryNoteDto?)null, "Phiếu nhập chưa có chứng từ giao hàng.");

        var deliveryNote = await _deliveryNoteRepository.FirstOrDefaultAsync(
            x => x.Id == order.DeliveryNoteId.Value && !x.IsDeleted, false, x => x.OriginalImageFile);
        if (deliveryNote == null)
            return ApiResponse.Success((DeliveryNoteDto?)null, "Phiếu nhập chưa có chứng từ giao hàng.");

        var imageUrl = deliveryNote.OriginalImageFile != null
            ? _storageService.GetOriginalUrl(deliveryNote.OriginalImageFile.FileKey)
            : null;

        return ApiResponse.Success(deliveryNote.ToDto(order.Id, imageUrl));
    }

    // ════════════════════════════════════════════════════════════════════
    // Non-paddy (PO → InboundOrder) flow
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ghi số lượng thực nhận cho từng dòng InboundOrderItem (non-paddy).
    /// Chỉ cập nhật QuantityReceived — chưa tăng tồn kho.
    /// Có thể gọi nhiều đợt cho đến khi gọi ConfirmNonPaddyReceiveAsync.
    /// </summary>
    public async Task<ApiResponse> ReceiveNonPaddyAsync(int id, ReceiveNonPaddyDto dto)
    {
        var order = await _inboundOrderRepository.FirstOrDefaultAsync(
            x => x.Id == id && !x.IsDeleted, true, x => x.InboundOrderItems);
        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

        if (order.SourceType != "PO")
            return ApiResponse.Conflict("API này chỉ áp dụng cho phiếu nhập từ đơn mua (SourceType=PO).",
                ApiCodeConstants.Common.BadRequest);

        var draftStatusId = await GetStatusIdAsync(InboundOrderStatusNames.Draft);
        var receivingId   = await GetStatusIdAsync(InboundOrderStatusNames.Receiving);
        var allowedStatus = new[] { draftStatusId, receivingId };

        if (!allowedStatus.Contains(order.InboundOrderStatusId))
            return ApiResponse.Conflict(
                "Phiếu nhập phải ở trạng thái Draft hoặc Receiving để ghi nhận hàng.",
                ApiCodeConstants.Common.UnprocessableEntity);

        var now    = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();

        foreach (var itemDto in dto.Items)
        {
            var item = order.InboundOrderItems.FirstOrDefault(i => !i.IsDeleted && i.Id == itemDto.InboundOrderItemId);
            if (item == null)
                return ApiResponse.BadRequest(
                    $"InboundOrderItem ID {itemDto.InboundOrderItemId} không tồn tại trong phiếu.",
                    ApiCodeConstants.Common.BadRequest);

            // Validate: số lượng nhận không được vượt số lượng còn lại
            var remaining = item.QuantityOrdered - item.QuantityReceived;
            if (itemDto.QuantityReceived > remaining)
                return ApiResponse.UnprocessableEntity(
                    $"Số lượng nhận ({itemDto.QuantityReceived}) vượt quá số lượng còn lại ({remaining}) " +
                    $"cho sản phẩm ID {item.ProductVariantId}.",
                    ApiCodeConstants.PurchaseOrder.QuantityExceedsRemain);

            item.QuantityReceived  += itemDto.QuantityReceived;
            item.ActualWeightKg     = itemDto.ActualWeightKg;
            item.Note               = itemDto.Note ?? item.Note;
            item.LastModifiedDate   = now;
            item.UpdatedBy          = userId;
            await _inboundOrderItemRepository.UpdateAsync(item);
        }

        // Chuyển trạng thái → Receiving nếu đang Draft
        if (order.InboundOrderStatusId == draftStatusId)
        {
            order.InboundOrderStatusId = receivingId;
            order.LastModifiedDate     = now;
            order.UpdatedBy            = userId;
            await _inboundOrderRepository.UpdateAsync(order);
        }

        await _inboundOrderRepository.SaveChangesAsync();
        return ApiResponse.Success(message: "Ghi nhận số lượng thực nhận thành công.");
    }

    /// <summary>
    /// Xác nhận hoàn tất nhập kho non-paddy — thực hiện trong 1 DB transaction.
    /// 1. Validate InboundOrder + SourceType=PO
    /// 2. Validate supplier, warehouse active
    /// 3. Validate QuantityReceived > 0 cho mỗi item
    /// 4. Tìm hoặc tạo Inventory per (variant + warehouse + location)
    /// 5. QuantityOnHand += QuantityReceived
    /// 6. Tạo InventoryTransaction(IMPORT, INBOUND_ORDER)
    /// 7. Nếu CreateBatchLot: tạo PaddyLot(LotType=PURCHASED_GOOD)
    /// 8. Cập nhật InboundOrder → Confirmed/Completed
    /// 9. Cập nhật PurchaseOrder → PartiallyReceived hoặc Received
    /// </summary>
    public async Task<ApiResponse> ConfirmNonPaddyReceiveAsync(int id, ConfirmNonPaddyReceiveDto dto)
    {
        // 1. Load InboundOrder
        var order = await _inboundOrderRepository.FirstOrDefaultAsync(
            x => x.Id == id && !x.IsDeleted, true,
            x => x.InboundOrderItems,
            x => x.Warehouse,
            x => x.PurchaseOrder);
        if (order == null)
            return ApiResponse.NotFound("Không tìm thấy phiếu nhập.", ApiCodeConstants.Common.NotFound);

        if (order.SourceType != "PO")
            return ApiResponse.Conflict("API này chỉ áp dụng cho phiếu nhập từ đơn mua (SourceType=PO).",
                ApiCodeConstants.Common.BadRequest);

        var receivingId = await GetStatusIdAsync(InboundOrderStatusNames.Receiving);
        if (order.InboundOrderStatusId != receivingId)
            return ApiResponse.Conflict(
                "Phiếu nhập phải ở trạng thái Receiving để xác nhận nhập kho.",
                ApiCodeConstants.Common.UnprocessableEntity);

        // 2. Validate warehouse
        var warehouse = await _warehouseRepository.FirstOrDefaultAsync(x => x.Id == order.WarehouseId && !x.IsDeleted && x.IsActive);
        if (warehouse == null)
            return ApiResponse.UnprocessableEntity("Kho hàng không còn hoạt động.", ApiCodeConstants.Common.UnprocessableEntity);

        // 3. Validate items đều có QuantityReceived > 0
        var activeItems = order.InboundOrderItems.Where(i => !i.IsDeleted).ToList();
        if (!activeItems.Any())
            return ApiResponse.UnprocessableEntity("Phiếu nhập không có dòng sản phẩm nào.", ApiCodeConstants.Common.UnprocessableEntity);

        foreach (var item in activeItems)
        {
            if (item.QuantityReceived <= 0)
                return ApiResponse.UnprocessableEntity(
                    $"Sản phẩm ID {item.ProductVariantId} chưa ghi nhận số lượng nhận (QuantityReceived = 0). " +
                    "Vui lòng gọi API /receive trước.",
                    ApiCodeConstants.Common.UnprocessableEntity);
        }

        var now    = DateTimeHelper.VietnamNow();
        var userId = GetCurrentUserId();
        decimal totalAssetValue = 0;

        await using var transaction = await _inboundOrderRepository.BeginTransactionAsync();
        try
        {
            // Load PurchaseOrder để tính trạng thái sau nhận
            var po = order.PurchaseOrder;
            if (po == null && order.PurchaseOrderId.HasValue)
                po = await _purchaseOrderRepository.FirstOrDefaultAsync(
                    x => x.Id == order.PurchaseOrderId.Value && !x.IsDeleted, true,
                    x => x.PurchaseOrderItems);

            // 4–7. Xử lý từng InboundOrderItem
            foreach (var item in activeItems)
            {
                int? locationId = null;

                // 7. Tạo PaddyLot nếu yêu cầu lưu theo batch
                int? paddyLotId = null;
                if (item.QRScanned)
                {
                    var defaultLotStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => !x.IsDeleted)
                        ?? throw new InvalidOperationException("Không tìm thấy LotStatus nào trong hệ thống.");

                    var lotCode = $"LOT-PUR-{now:yyyyMMdd}-{item.Id:D5}";
                    var lot = new PaddyLot
                    {
                        OrganizationId    = order.OrganizationId,
                        LotCode           = lotCode,
                        LotType           = LotTypeConstants.PurchasedGood,
                        ProductVariantId  = item.ProductVariantId!.Value,
                        StatusId          = defaultLotStatus.Id,
                        WarehouseId       = order.WarehouseId,
                        LocationId        = locationId,
                        InboundDate       = now,
                        InitialWeightKg   = item.QuantityReceived,
                        RemainingWeightKg = item.QuantityReceived,
                        CostPricePerKg    = item.UnitCostPrice,
                        QrCode            = "PL-" + Guid.NewGuid().ToString("N").ToUpper(),
                        CreatedBy         = userId,
                        CreatedDate       = now
                    };

                    await _paddyLotRepository.CreateAsync(lot);
                    // Lưu tạm để lấy Id của Lot (chưa commit transaction)
                    await _inboundOrderRepository.SaveChangesAsync();

                    paddyLotId = lot.Id;
                    item.PaddyLotId = lot.Id;
                }

                // Ghi chú C2/M5: KHÔNG tự tạo Inventory row rỗng (QuantityOnHand=0, LocationId=null) và KHÔNG tạo InventoryTransaction tại đây.
                // Bản ghi Inventory thực tế và giao dịch nhập kho sẽ do PutawaySuggestionService.ConfirmStoreInAsync tự tìm/tạo khi xếp vào vị trí thực tế.

                // Ghi chú C2: KHÔNG tăng inv.QuantityOnHand và KHÔNG tạo InventoryTransaction tại đây.
                // Việc tăng tồn vật lý và tạo giao dịch nhập kho (Import) sẽ do PutawaySuggestionService.ConfirmStoreInAsync thực hiện.

                totalAssetValue += item.QuantityReceived * item.UnitCostPrice;
                item.LastModifiedDate = now;
                item.UpdatedBy        = userId;
                await _inboundOrderItemRepository.UpdateAsync(item);
            }

            // 8. Cập nhật InboundOrder → Confirmed
            var confirmedStatusId          = await GetStatusIdAsync(InboundOrderStatusNames.Confirmed);
            order.InboundOrderStatusId     = confirmedStatusId;
            order.TotalAssetValue          = totalAssetValue;
            order.CompletedDate            = now;
            order.LastModifiedDate         = now;
            order.UpdatedBy                = userId;
            if (dto.Note != null) order.Note = dto.Note;
            await _inboundOrderRepository.UpdateAsync(order);

            // 9. Cập nhật PurchaseOrder status
            if (po != null)
            {
                // Tính tổng đã nhận theo tất cả InboundOrder liên kết
                var allInboundOrders = await _inboundOrderRepository
                    .FindByCondition(x => x.PurchaseOrderId == po.Id && !x.IsDeleted, false, x => x.InboundOrderItems)
                    .ToListAsync();

                var receivedByPoItem = allInboundOrders
                    .SelectMany(io => io.InboundOrderItems)
                    .Where(ii => !ii.IsDeleted && ii.PurchaseOrderItemId.HasValue)
                    .GroupBy(ii => ii.PurchaseOrderItemId!.Value)
                    .ToDictionary(g => g.Key, g => g.Sum(ii => ii.QuantityReceived));

                bool allReceived = po.PurchaseOrderItems.Where(i => !i.IsDeleted).All(i =>
                {
                    receivedByPoItem.TryGetValue(i.Id, out var received);
                    return received >= i.QuantityOrdered;
                });

                var newPoStatusName = allReceived
                    ? PurchaseOrderStatusNames.Received
                    : PurchaseOrderStatusNames.PartiallyReceived;

                var poStatus = await _purchaseOrderStatusRepository.FirstOrDefaultAsync(
                    x => x.Name == newPoStatusName && !x.IsDeleted);
                if (poStatus != null)
                {
                    po.StatusId         = poStatus.Id;
                    po.LastModifiedDate = now;
                    po.UpdatedBy        = userId;
                    await _purchaseOrderRepository.UpdateAsync(po);
                }
            }

            // Ghi nhận tất cả thay đổi đồng loạt (C3)
            await _inboundOrderRepository.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "ConfirmNonPaddyReceiveAsync failed for InboundOrder {Id}", id);
            return ApiResponse.BadRequest($"Lỗi xử lý: {ex.Message}", ApiCodeConstants.Common.BadRequest);
        }

        return ApiResponse.Success(
            new { TotalAssetValue = totalAssetValue },
            "Xác nhận nhập kho thành công.");
    }
}

