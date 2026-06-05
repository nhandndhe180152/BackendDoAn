using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.ProductVariants;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// Lớp triển khai nghiệp vụ liên quan đến Biến thể sản phẩm (Product Variant) và các tính năng quét mã QR
public class ProductVariantService : IProductVariantService
{
    private readonly IProductVariantRepository _productVariantRepository;
    private readonly IStorageService _storageService;
    private readonly IRepositoryBase<Inventory, int> _inventoryRepository;
    private readonly IRepositoryBase<PurchaseOrderItem, int> _purchaseOrderItemRepository;
    private readonly IRepositoryBase<SalesOrderItem, int> _salesOrderItemRepository;
    private readonly IRepositoryBase<StockTakeItem, int> _stockTakeItemRepository;

    /// Khởi tạo ProductVariantService
    public ProductVariantService(
        IProductVariantRepository productVariantRepository,
        IStorageService storageService,
        IRepositoryBase<Inventory, int> inventoryRepository,
        IRepositoryBase<PurchaseOrderItem, int> purchaseOrderItemRepository,
        IRepositoryBase<SalesOrderItem, int> salesOrderItemRepository,
        IRepositoryBase<StockTakeItem, int> stockTakeItemRepository)
    {
        _productVariantRepository = productVariantRepository;
        _storageService = storageService;
        _inventoryRepository = inventoryRepository;
        _purchaseOrderItemRepository = purchaseOrderItemRepository;
        _salesOrderItemRepository = salesOrderItemRepository;
        _stockTakeItemRepository = stockTakeItemRepository;
    }

    /// Tạo mới một biến thể sản phẩm
    public async Task<ApiResponse> CreateAsync(CreateProductVariantDto obj)
    {
        var model = obj.ToEntity();
        
        // Kiểm tra xem mã SKU đã tồn tại trong hệ thống chưa
        var isExistingSKU = await _productVariantRepository.AnyAsync(
            x => x.SKU.ToLower() == model.SKU.ToLower() && !x.IsDeleted);

        if (isExistingSKU)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.SKU),
                ApiCodeConstants.Common.DuplicatedData
            );

        await _productVariantRepository.CreateAsync(model);
        await _productVariantRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id);
    }

    /// Tạo danh sách nhiều biến thể sản phẩm
    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateProductVariantDto> objs)
    {
        var models = objs.Select(x => x.ToEntity()).ToList();
        await _productVariantRepository.CreateListAsync(models);
        await _productVariantRepository.SaveChangesAsync();
        return ApiResponse.Created(models.Select(x => x.Id));
    }

    /// Lấy toàn bộ danh sách biến thể sản phẩm, tự động chuyển đổi file key thành URL ảnh đầy đủ
    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _productVariantRepository
            .FindByCondition(x => !x.IsDeleted)
            .Include(x => x.Product)
            .Include(x => x.UnitOfMeasure)
            .Include(x => x.Image)
            .ToListAsync();

        var dtos = data.Select(x => x.ToDto(x.Image != null ? _storageService.GetOriginalUrl(x.Image.FileKey) : null)).ToList();
        return ApiResponse.Success(dtos);
    }

    /// Lấy thông tin chi tiết một biến thể sản phẩm theo ID kèm ảnh từ Cloudinary/Storage
    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _productVariantRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted)
            .Include(x => x.Product)
            .Include(x => x.UnitOfMeasure)
            .Include(x => x.Image)
            .FirstOrDefaultAsync();

        if (data == null)
            return ApiResponse.NotFound();

        var imageUrl = data.Image != null ? _storageService.GetOriginalUrl(data.Image.FileKey) : null;
        return ApiResponse.Success(data.ToDto(imageUrl));
    }

    /// Tìm kiếm phân trang cơ bản theo từ khóa (Mã SKU, tên sản phẩm gốc, tên biến thể...)
    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = await _productVariantRepository
            .FindByCondition(x => !x.IsDeleted)
            .Include(x => x.Product)
            .Include(x => x.UnitOfMeasure)
            .Include(x => x.Image)
            .ToListAsync();

        var mapped = data.Select(x => x.ToDto(x.Image != null ? _storageService.GetOriginalUrl(x.Image.FileKey) : null));

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            mapped = mapped.Where(x => x.Name.ToLower().Contains(query.Keyword.ToLower()) ||
                                       x.Description != null && x.Description.ToLower().Contains(query.Keyword.ToLower()) ||
                                       x.SKU.ToLower().Contains(query.Keyword.ToLower()) ||
                                       x.ProductName != null && x.ProductName.ToLower().Contains(query.Keyword.ToLower()));
        }

        if (!string.IsNullOrEmpty(query.OrderBy))
        {
            mapped = mapped.AsQueryable().OrderByDynamic(query.OrderBy, query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);
        }

        var totalRecord = mapped.Count();
        var pagedList = mapped.Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).ToList();

        var pagedData = new PagingData<ProductVariantDetailDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = pagedList,
            Total = totalRecord,
            TotalFiltered = totalRecord
        };

        return ApiResponse.Success(pagedData);
    }

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    /// Phân trang nâng cao tích hợp bộ lọc Datatable ở Repository
    public async Task<ApiResponse> GetPagedAsync(ProductVariantDTParameters parameters)
    {
        var result = await _productVariantRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(result);
    }

    /// Xóa mềm một biến thể sản phẩm theo ID (IsDeleted = true)
    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _productVariantRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _productVariantRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    /// Cập nhật thông tin một biến thể sản phẩm
    public async Task<ApiResponse> UpdateAsync(UpdateProductVariantDto obj)
    {
        var existData = await _productVariantRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();

        // Kiểm tra xem SKU mới có trùng với SKU của biến thể khác không
        var isDuplicatedSKU = await _productVariantRepository.AnyAsync(
            x => x.SKU.ToLower() == obj.SKU.ToLower() && x.Id != obj.Id && !x.IsDeleted);

        if (isDuplicatedSKU)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.SKU),
                ApiCodeConstants.Common.DuplicatedData
            );

        obj.ToEntity(existData);
        await _productVariantRepository.UpdateAsync(existData);
        await _productVariantRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateProductVariantDto> obj)
    {
        throw new NotImplementedException();
    }

    /// Lọc phân trang biến thể sản phẩm theo ProductId và trạng thái hoạt động
    public async Task<ApiResponse> GetPagedAsync(ProductVariantSearchQuery query)
    {
        var data = _productVariantRepository
            .FindByCondition(x => !x.IsDeleted)
            .Include(x => x.Product)
            .Include(x => x.UnitOfMeasure)
            .Include(x => x.Image);

        var totalRecord = await data.CountAsync();

        var mapped = await data.Select(x => x.ToDto(x.Image != null ? _storageService.GetOriginalUrl(x.Image.FileKey) : null)).ToListAsync();

        var queryable = mapped.AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            queryable = queryable.Where(x => x.Name.ToLower().Contains(query.Keyword.ToLower()) ||
                                             x.Description != null && x.Description.ToLower().Contains(query.Keyword.ToLower()) ||
                                             x.SKU.ToLower().Contains(query.Keyword.ToLower()) ||
                                             x.ProductName != null && x.ProductName.ToLower().Contains(query.Keyword.ToLower()));
        }

        if (query.ProductId.HasValue)
        {
            queryable = queryable.Where(x => x.ProductId == query.ProductId.Value);
        }

        if (query.IsActive.HasValue)
        {
            queryable = queryable.Where(x => x.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrEmpty(query.OrderBy))
        {
            queryable = queryable.OrderByDynamic(query.OrderBy, query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);
        }

        var totalFiltered = queryable.Count();
        var pagedList = queryable.Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).ToList();

        var pagedData = new PagingData<ProductVariantDetailDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = pagedList,
            Total = totalRecord,
            TotalFiltered = totalFiltered
        };

        return ApiResponse.Success(pagedData);
    }

    /// Kiểm tra SKU sản phẩm và tính toán số lượng tồn kho (Thực tế, Đã giữ chỗ, Khả dụng).
    /// Hỗ trợ kiểm tra xem SKU này có thuộc một phiếu tài liệu cụ thể (Mua hàng, Bán hàng, Kiểm kho) hay không.
    public async Task<ApiResponse> CheckSkuAsync(string sku, string? documentType = null, int? documentId = null)
    {
        var variant = await _productVariantRepository
            .FindByCondition(x => x.SKU.ToLower() == sku.ToLower() && !x.IsDeleted)
            .Include(x => x.Product)
            .Include(x => x.UnitOfMeasure)
            .Include(x => x.Image)
            .FirstOrDefaultAsync();

        if (variant == null)
        {
            return ApiResponse.NotFound(message: $"Không tìm thấy biến thể sản phẩm với SKU '{sku}'.");
        }

        var imageUrl = variant.Image != null ? _storageService.GetOriginalUrl(variant.Image.FileKey) : null;
        var variantDto = variant.ToDto(imageUrl);

        // Lấy tất cả thông tin tồn kho của biến thể này tại các kho hàng
        var inventories = await _inventoryRepository
            .FindByCondition(x => x.ProductVariantId == variant.Id)
            .ToListAsync();

        int qtyOnHand = inventories.Sum(x => x.QuantityOnHand); // Số lượng tồn kho thực tế
        int qtyReserved = inventories.Sum(x => x.QuantityReserved); // Số lượng đã đặt trước (chờ giao)
        int qtyAvailable = qtyOnHand - qtyReserved; // Số lượng khả dụng có thể bán

        var result = new SkuCheckResultDto
        {
            ProductVariant = variantDto,
            QuantityOnHand = qtyOnHand,
            QuantityReserved = qtyReserved,
            QuantityAvailable = qtyAvailable,
            BelongsToDocument = false,
            Message = "Tìm thấy sản phẩm. Chưa xác minh với phiếu."
        };

        // Nếu có truyền kèm tài liệu liên quan, kiểm tra xem biến thể này có thuộc chứng từ đó không
        if (!string.IsNullOrEmpty(documentType) && documentId.HasValue)
        {
            if (documentType.Equals("PurchaseOrder", StringComparison.OrdinalIgnoreCase))
            {
                // Kiểm tra xem sản phẩm có nằm trong Phiếu mua hàng (Nhập kho) không
                var poItem = await _purchaseOrderItemRepository
                    .FindByCondition(x => x.PurchaseOrderId == documentId.Value && x.ProductVariantId == variant.Id && !x.IsDeleted)
                    .FirstOrDefaultAsync();

                if (poItem != null)
                {
                    result.BelongsToDocument = true;
                    result.DocumentQuantityOrdered = poItem.QuantityOrdered;
                    result.DocumentQuantityProcessed = poItem.QuantityReceived;
                    result.IsQrScanned = poItem.QRScanned;
                    result.Message = "Sản phẩm hợp lệ và thuộc Phiếu mua hàng (Purchase Order).";
                }
                else
                {
                    result.Message = "Sản phẩm KHÔNG thuộc Phiếu mua hàng này.";
                }
            }
            else if (documentType.Equals("SalesOrder", StringComparison.OrdinalIgnoreCase))
            {
                // Kiểm tra xem sản phẩm có nằm trong Đơn bán hàng (Xuất kho) không
                var soItem = await _salesOrderItemRepository
                    .FindByCondition(x => x.SalesOrderId == documentId.Value && x.ProductVariantId == variant.Id && !x.IsDeleted)
                    .FirstOrDefaultAsync();

                if (soItem != null)
                {
                    result.BelongsToDocument = true;
                    result.DocumentQuantityOrdered = soItem.QuantityOrdered;
                    result.DocumentQuantityProcessed = soItem.QuantityPicked;
                    result.IsQrScanned = soItem.QRScanned;
                    result.Message = "Sản phẩm hợp lệ và thuộc Đơn bán hàng (Sales Order).";
                }
                else
                {
                    result.Message = "Sản phẩm KHÔNG thuộc Đơn bán hàng này.";
                }
            }
            else if (documentType.Equals("StockTake", StringComparison.OrdinalIgnoreCase))
            {
                // Kiểm tra xem sản phẩm có nằm trong Phiếu kiểm kho không
                var stItem = await _stockTakeItemRepository
                    .FindByCondition(x => x.StockTakeId == documentId.Value && x.ProductVariantId == variant.Id && !x.IsDeleted)
                    .FirstOrDefaultAsync();

                if (stItem != null)
                {
                    result.BelongsToDocument = true;
                    result.DocumentQuantityOrdered = stItem.SystemQuantity;
                    result.DocumentQuantityProcessed = stItem.ActualQuantity ?? 0;
                    result.IsQrScanned = stItem.QRScanned;
                    result.Message = "Sản phẩm hợp lệ và thuộc Phiếu kiểm kho (Stock Take).";
                }
                else
                {
                    result.Message = "Sản phẩm KHÔNG thuộc Phiếu kiểm kho này.";
                }
            }
            else
            {
                result.Message = $"Loại tài liệu '{documentType}' không được hỗ trợ để xác minh.";
            }
        }

        return ApiResponse.Success(result);
    }

    /// Xác nhận kết quả quét mã QR và cập nhật số lượng đã xử lý (đã nhận/đã lấy/thực tế) của biến thể trong chứng từ
    public async Task<ApiResponse> ConfirmScanAsync(ConfirmScanRequestDto request)
    {
        if (request == null)
            return ApiResponse.BadRequest(message: "Yêu cầu không hợp lệ.");

        var variant = await _productVariantRepository
            .FindByCondition(x => x.SKU.ToLower() == request.Sku.ToLower() && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (variant == null)
            return ApiResponse.NotFound(message: $"Không tìm thấy biến thể sản phẩm với SKU '{request.Sku}'.");

        if (request.DocumentType.Equals("PurchaseOrder", StringComparison.OrdinalIgnoreCase))
        {
            var poItem = await _purchaseOrderItemRepository
                .FindByCondition(x => x.PurchaseOrderId == request.DocumentId && x.ProductVariantId == variant.Id && !x.IsDeleted)
                .FirstOrDefaultAsync();

            if (poItem == null)
                return ApiResponse.BadRequest(message: $"Sản phẩm với SKU '{request.Sku}' không thuộc Phiếu mua hàng ID {request.DocumentId}.");

            poItem.QRScanned = true;
            poItem.QuantityReceived += request.Quantity;
            poItem.LastModifiedDate = DateTime.Now;
            
            await _purchaseOrderItemRepository.UpdateAsync(poItem);
            await _purchaseOrderItemRepository.SaveChangesAsync();

            return ApiResponse.Success(message: "Xác nhận quét mã QR nhập kho thành công.");
        }
        else if (request.DocumentType.Equals("SalesOrder", StringComparison.OrdinalIgnoreCase))
        {
            var soItem = await _salesOrderItemRepository
                .FindByCondition(x => x.SalesOrderId == request.DocumentId && x.ProductVariantId == variant.Id && !x.IsDeleted)
                .FirstOrDefaultAsync();

            if (soItem == null)
                return ApiResponse.BadRequest(message: $"Sản phẩm với SKU '{request.Sku}' không thuộc Đơn bán hàng ID {request.DocumentId}.");

            soItem.QRScanned = true;
            soItem.QuantityPicked += request.Quantity;
            soItem.LastModifiedDate = DateTime.Now;

            await _salesOrderItemRepository.UpdateAsync(soItem);
            await _salesOrderItemRepository.SaveChangesAsync();

            return ApiResponse.Success(message: "Xác nhận quét mã QR xuất kho thành công.");
        }
        else if (request.DocumentType.Equals("StockTake", StringComparison.OrdinalIgnoreCase))
        {
            var stItem = await _stockTakeItemRepository
                .FindByCondition(x => x.StockTakeId == request.DocumentId && x.ProductVariantId == variant.Id && !x.IsDeleted)
                .FirstOrDefaultAsync();

            if (stItem == null)
                return ApiResponse.BadRequest(message: $"Sản phẩm với SKU '{request.Sku}' không thuộc Phiếu kiểm kho ID {request.DocumentId}.");

            stItem.QRScanned = true;
            stItem.ActualQuantity = (stItem.ActualQuantity ?? 0) + request.Quantity;
            stItem.LastModifiedDate = DateTime.Now;

            await _stockTakeItemRepository.UpdateAsync(stItem);
            await _stockTakeItemRepository.SaveChangesAsync();

            return ApiResponse.Success(message: "Xác nhận quét mã QR kiểm kho thành công.");
        }

        return ApiResponse.BadRequest(message: $"Loại tài liệu '{request.DocumentType}' không được hỗ trợ để xác minh.");
    }
}
