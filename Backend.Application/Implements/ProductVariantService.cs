using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DependencyInjection.Extentions;
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

/// Lớp triển khai nghiệp vụ liên quan đến Biến thể sản phẩm (Product Variant)
public class ProductVariantService : IProductVariantService
{
    private readonly IProductVariantRepository _productVariantRepository;
    private readonly IProductRepository _productRepository;
    private readonly IProductCategoryRepository _productCategoryRepository;
    private readonly IProductAttributeRepository _productAttributeRepository;
    private readonly IRepositoryBase<FileUpload, int> _fileUploadRepository;
    private readonly IRepositoryBase<UnitOfMeasure, int> _uomRepository;
    private readonly IStorageService _storageService;
    private readonly IRepositoryBase<Inventory, int> _inventoryRepository;
    private readonly IRepositoryBase<InboundOrderItem, int> _inboundOrderItemRepository;
    private readonly IRepositoryBase<OutboundOrderItem, int> _outboundOrderItemRepository;
    private readonly IRepositoryBase<StockTakeItem, int> _stockTakeItemRepository;
    private readonly IQRCodeService _qrCodeService;

    /// Khởi tạo ProductVariantService
    public ProductVariantService(
        IProductVariantRepository productVariantRepository,
        IProductRepository productRepository,
        IProductCategoryRepository productCategoryRepository,
        IProductAttributeRepository productAttributeRepository,
        IRepositoryBase<FileUpload, int> fileUploadRepository,
        IRepositoryBase<UnitOfMeasure, int> uomRepository,
        IStorageService storageService,
        IRepositoryBase<Inventory, int> inventoryRepository,
        IRepositoryBase<InboundOrderItem, int> inboundOrderItemRepository,
        IRepositoryBase<OutboundOrderItem, int> outboundOrderItemRepository,
        IRepositoryBase<StockTakeItem, int> stockTakeItemRepository,
        IQRCodeService qrCodeService)
    {
        _productVariantRepository = productVariantRepository;
        _productRepository = productRepository;
        _productCategoryRepository = productCategoryRepository;
        _productAttributeRepository = productAttributeRepository;
        _fileUploadRepository = fileUploadRepository;
        _uomRepository = uomRepository;
        _storageService = storageService;
        _inventoryRepository = inventoryRepository;
        _inboundOrderItemRepository = inboundOrderItemRepository;
        _outboundOrderItemRepository = outboundOrderItemRepository;
        _stockTakeItemRepository = stockTakeItemRepository;
        _qrCodeService = qrCodeService;
    }

    // ──────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────
    /// Validate AttributeValues JSON, returns error message or null if valid.
    private async Task<string?> ValidateAttributeValuesAsync(string? attributeValuesJson)
    {
        if (string.IsNullOrWhiteSpace(attributeValuesJson))
            return null;

        List<RawAttributeEntry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<RawAttributeEntry>>(attributeValuesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return "AttributeValues phải là JSON hợp lệ hoặc để trống.";
        }

        if (entries == null || entries.Count == 0)
            return null;

        var ids = entries.Select(e => e.AttributeId).ToList();
        if (ids.Count != ids.Distinct().Count())
            return "AttributeValues chứa attributeId bị trùng lặp.";

        foreach (var e in entries)
        {
            if (string.IsNullOrWhiteSpace(e.Value))
                return $"Giá trị của attributeId={e.AttributeId} không được để trống.";
        }

        // Single IN(...) query — tránh N lần GetByIdAsync
        var existingIds = await _productAttributeRepository
            .FindByCondition(x => ids.Contains(x.Id) && !x.IsDeleted)
            .Select(x => x.Id)
            .ToListAsync();

        var missingIds = ids.Where(id => !existingIds.Contains(id)).ToList();
        if (missingIds.Count > 0)
            return $"Một hoặc nhiều thuộc tính không tồn tại hoặc đã bị xóa (IDs: {string.Join(", ", missingIds)}).";

        return null;
    }

    private class RawAttributeEntry
    {
        public int AttributeId { get; set; }
        public string? Value { get; set; }
    }

    /// Parse attribute IDs from JSON (pure CPU, no DB)
    private static List<int> ParseAttributeIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<int>();
        try
        {
            var entries = JsonSerializer.Deserialize<List<RawAttributeEntry>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return entries?.Select(e => e.AttributeId).ToList() ?? new List<int>();
        }
        catch { return new List<int>(); }
    }

    /// Load tên attribute cho cả page bằng 1 query WHERE Id IN (...)
    private async Task<Dictionary<int, string>> LoadAttrNamesAsync(IEnumerable<ProductVariant> variants)
    {
        var ids = variants
            .SelectMany(v => ParseAttributeIds(v.AttributeValues))
            .Distinct().ToList();

        if (ids.Count == 0) return new Dictionary<int, string>();

        return await _productAttributeRepository
            .FindByCondition(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);
    }

    /// Dựng lookup cho 1 variant dùng shared dict đã load sẵn
    private static Dictionary<int, string> BuildAttributeLookup(
        string? json, Dictionary<int, string> sharedAttrNames)
    {
        var lookup = new Dictionary<int, string>();
        if (string.IsNullOrWhiteSpace(json)) return lookup;
        try
        {
            var entries = JsonSerializer.Deserialize<List<RawAttributeEntry>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (entries == null) return lookup;
            foreach (var e in entries)
                if (sharedAttrNames.TryGetValue(e.AttributeId, out var name))
                    lookup[e.AttributeId] = name;
        }
        catch { }
        return lookup;
    }

    /// Map entity + shared attrNames -> DTO (sync)
    private ProductVariantDetailDto MapWithLookup(
        ProductVariant entity, Dictionary<int, string> sharedAttrNames)
    {
        var imageUrl = entity.Image != null ? _storageService.GetOriginalUrl(entity.Image.FileKey) : null;
        var attrLookup = BuildAttributeLookup(entity.AttributeValues, sharedAttrNames);
        bool categoryDeleted = entity.Product?.ProductCategory?.IsDeleted ?? false;
        return entity.ToDto(imageUrl, categoryDeleted, attrLookup);
    }

    // ──────────────────────────────────────────────────────────
    // CREATE
    // ──────────────────────────────────────────────────────────
    /// Tạo mới một biến thể sản phẩm với đầy đủ validation theo tài liệu
    public async Task<ApiResponse> CreateAsync(CreateProductVariantDto obj)
    {
        // Validate Product
        var product = await _productRepository
            .FindByCondition(x => x.Id == obj.ProductId && !x.IsDeleted)
            .Include(x => x.ProductCategory)
            .FirstOrDefaultAsync();

        if (product == null)
            return ApiResponse.NotFound(message: "Sản phẩm không tồn tại hoặc đã bị xóa.");
        if (!product.IsActive)
            return ApiResponse.UnprocessableEntity(
                "Sản phẩm đang bị vô hiệu hóa, không thể thêm biến thể.",
                ApiCodeConstants.Common.InvalidData);

        // Validate UoM
        var uom = await _uomRepository.GetByIdAsync(obj.UnitOfMeasureId);
        if (uom == null || uom.IsDeleted)
            return ApiResponse.UnprocessableEntity(
                "Đơn vị tính không tồn tại hoặc đã bị xóa.",
                ApiCodeConstants.Common.InvalidData);

        // Validate ImageId
        if (obj.ImageId.HasValue)
        {
            var image = await _fileUploadRepository.GetByIdAsync(obj.ImageId.Value);
            if (image == null || image.IsDeleted)
                return ApiResponse.UnprocessableEntity(
                    "Hình ảnh không tồn tại hoặc đã bị xóa.",
                    ApiCodeConstants.Common.InvalidData);
        }

        // Normalize & validate SKU
        var sku = obj.SKU?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(sku))
            return ApiResponse.BadRequest(message: "SKU không được để trống.");

        var isExistingSKU = await _productVariantRepository.AnyAsync(x => x.SKU == sku && !x.IsDeleted);
        if (isExistingSKU)
            return ApiResponse.Conflict(
                $"SKU '{sku}' đã tồn tại trong hệ thống.",
                ApiCodeConstants.Common.DuplicatedData);

        // Validate QRCode uniqueness
        if (!string.IsNullOrWhiteSpace(obj.QRCode))
        {
            var qrExists = await _productVariantRepository.AnyAsync(
                x => x.QRCode == obj.QRCode && !x.IsDeleted);
            if (qrExists)
                return ApiResponse.Conflict(
                    $"QRCode '{obj.QRCode}' đã tồn tại trong hệ thống.",
                    ApiCodeConstants.Common.DuplicatedData);
        }

        // Validate numeric rules
        if (obj.CostPrice < 0)
            return ApiResponse.UnprocessableEntity("CostPrice phải lớn hơn hoặc bằng 0.", ApiCodeConstants.Common.InvalidData);
        if (obj.Weight < 0)
            return ApiResponse.UnprocessableEntity("Khối lượng (Weight) phải lớn hơn hoặc bằng 0.", ApiCodeConstants.Common.InvalidData);
        
        if (obj.MinStockLevel.HasValue && obj.MinStockLevel.Value < 0)
            return ApiResponse.UnprocessableEntity("MinStockLevel phải lớn hơn hoặc bằng 0.", ApiCodeConstants.Common.InvalidData);
        if (obj.IsIoTRequired && obj.Weight <= 0)
            return ApiResponse.UnprocessableEntity("Khi IsIoTRequired = true, Weight phải lớn hơn 0.", ApiCodeConstants.Common.InvalidData);

        // Validate AttributeValues
        var attrError = await ValidateAttributeValuesAsync(obj.AttributeValues);
        if (attrError != null)
            return ApiResponse.UnprocessableEntity(attrError, ApiCodeConstants.Common.InvalidData);

        var model = obj.ToEntity();
        model.SKU = sku;
        model.IsDeleted = false;

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

    // ──────────────────────────────────────────────────────────
    // READ
    // ──────────────────────────────────────────────────────────
    private async Task<ProductVariantDetailDto> MapToDto(ProductVariant entity)
    {
        var sharedAttrNames = await LoadAttrNamesAsync(new[] { entity });
        return MapWithLookup(entity, sharedAttrNames);
    }

    /// Lấy toàn bộ danh sách biến thể sản phẩm
    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _productVariantRepository
            .FindByCondition(x => !x.IsDeleted)
            .Include(x => x.Product).ThenInclude(p => p.ProductCategory)
            .Include(x => x.UnitOfMeasure)
            .Include(x => x.Image)
            .ToListAsync();

        var sharedAttrNames = await LoadAttrNamesAsync(data);
        var dtos = data.Select(v => MapWithLookup(v, sharedAttrNames)).ToList();
        return ApiResponse.Success(dtos);
    }

    /// Lấy thông tin chi tiết một biến thể sản phẩm theo ID
    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _productVariantRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted)
            .Include(x => x.Product).ThenInclude(p => p.ProductCategory)
            .Include(x => x.UnitOfMeasure)
            .Include(x => x.Image)
            .FirstOrDefaultAsync();

        if (data == null)
            return ApiResponse.NotFound();

        return ApiResponse.Success(await MapToDto(data));
    }

    /// Tra cứu biến thể theo QRCode
    public async Task<ApiResponse> GetByQrCodeAsync(string qrCode)
    {
        var data = await _productVariantRepository
            .FindByCondition(x => x.QRCode == qrCode && !x.IsDeleted)
            .Include(x => x.Product).ThenInclude(p => p.ProductCategory)
            .Include(x => x.UnitOfMeasure)
            .Include(x => x.Image)
            .FirstOrDefaultAsync();

        if (data == null)
            return ApiResponse.NotFound(message: $"Không tìm thấy biến thể với QRCode '{qrCode}'.");

        return ApiResponse.Success(await MapToDto(data));
    }

    /// Lấy URL QR code đã lưu của biến thể theo ID (chỉ trả về field QRCode)
    public async Task<ApiResponse> GetQrCodeUrlAsync(int id)
    {
        var exists = await _productVariantRepository.AnyAsync(x => x.Id == id && !x.IsDeleted);
        if (!exists)
            return ApiResponse.NotFound();

        var qrCode = await _productVariantRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted)
            .Select(x => x.QRCode)
            .FirstOrDefaultAsync();

        return ApiResponse.Success(new { QRCode = qrCode });
    }

    /// Tìm kiếm phân trang cơ bản
    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var baseQuery = _productVariantRepository
            .FindByCondition(x => !x.IsDeleted);

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword.ToLower();
            baseQuery = baseQuery.Where(x =>
                x.Name.ToLower().Contains(kw) ||
                (x.Description != null && x.Description.ToLower().Contains(kw)) ||
                x.SKU.ToLower().Contains(kw) ||
                x.Product.Name.ToLower().Contains(kw));
        }

        var totalRecord = await baseQuery.CountAsync();

        if (!string.IsNullOrEmpty(query.OrderBy))
            baseQuery = baseQuery.OrderByDynamic(query.OrderBy,
                query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);

        var pageData = await baseQuery
            .Include(x => x.Product).ThenInclude(p => p.ProductCategory)
            .Include(x => x.UnitOfMeasure)
            .Include(x => x.Image)
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        var sharedAttrNames = await LoadAttrNamesAsync(pageData);
        var dtos = pageData.Select(v => MapWithLookup(v, sharedAttrNames)).ToList();

        return ApiResponse.Success(new PagingData<ProductVariantDetailDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = dtos,
            Total = totalRecord,
            TotalFiltered = totalRecord
        });
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

    /// Lọc phân trang biến thể sản phẩm theo nhiều tiêu chí
    public async Task<ApiResponse> GetPagedAsync(ProductVariantSearchQuery query)
    {
        IQueryable<ProductVariant> baseQuery;

        if (!query.IncludeDeleted)
            baseQuery = _productVariantRepository.FindByCondition(x => !x.IsDeleted);
        else
            baseQuery = _productVariantRepository.FindByCondition(x => true);

        if (query.ProductId.HasValue)
            baseQuery = baseQuery.Where(x => x.ProductId == query.ProductId.Value);

        if (!string.IsNullOrWhiteSpace(query.Sku))
            baseQuery = baseQuery.Where(x => x.SKU.Contains(query.Sku.Trim().ToUpperInvariant()));

        if (!string.IsNullOrWhiteSpace(query.QrCode))
            baseQuery = baseQuery.Where(x => x.QRCode == query.QrCode.Trim());

        if (query.IsActive.HasValue)
            baseQuery = baseQuery.Where(x => x.IsActive == query.IsActive.Value);

        if (query.IsIoTRequired.HasValue)
            baseQuery = baseQuery.Where(x => x.IsIoTRequired == query.IsIoTRequired.Value);

        if (query.HasMinStockLevel.HasValue)
        {
            if (query.HasMinStockLevel.Value)
                baseQuery = baseQuery.Where(x => x.MinStockLevel != null);
            else
                baseQuery = baseQuery.Where(x => x.MinStockLevel == null);
        }

        if (query.ProductCategoryId.HasValue)
            baseQuery = baseQuery.Where(x => x.Product.ProductCategoryId == query.ProductCategoryId.Value);

        // Keyword filter at DB level
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword.ToLower();
            baseQuery = baseQuery.Where(x =>
                x.Name.ToLower().Contains(kw) ||
                (x.Description != null && x.Description.ToLower().Contains(kw)) ||
                x.SKU.ToLower().Contains(kw) ||
                x.Product.Name.ToLower().Contains(kw));
        }

        var totalFiltered = await baseQuery.CountAsync();

        if (!string.IsNullOrEmpty(query.OrderBy))
            baseQuery = baseQuery.OrderByDynamic(query.OrderBy,
                query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);

        var pageData = await baseQuery
            .Include(x => x.Product).ThenInclude(p => p.ProductCategory)
            .Include(x => x.UnitOfMeasure)
            .Include(x => x.Image)
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        var sharedAttrNames = await LoadAttrNamesAsync(pageData);
        var dtos = pageData.Select(v => MapWithLookup(v, sharedAttrNames)).ToList();

        return ApiResponse.Success(new PagingData<ProductVariantDetailDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = dtos,
            Total = totalFiltered,
            TotalFiltered = totalFiltered
        });
    }

    // ──────────────────────────────────────────────────────────
    // UPDATE
    // ──────────────────────────────────────────────────────────
    /// Cập nhật thông tin một biến thể sản phẩm
    public async Task<ApiResponse> UpdateAsync(UpdateProductVariantDto obj)
    {
        var existData = await _productVariantRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();

        // Validate UoM
        var uom = await _uomRepository.GetByIdAsync(obj.UnitOfMeasureId);
        if (uom == null || uom.IsDeleted)
            return ApiResponse.UnprocessableEntity(
                "Đơn vị tính không tồn tại hoặc đã bị xóa.",
                ApiCodeConstants.Common.InvalidData);

        // Validate ImageId
        if (obj.ImageId.HasValue)
        {
            var image = await _fileUploadRepository.GetByIdAsync(obj.ImageId.Value);
            if (image == null || image.IsDeleted)
                return ApiResponse.UnprocessableEntity(
                    "Hình ảnh không tồn tại hoặc đã bị xóa.",
                    ApiCodeConstants.Common.InvalidData);
        }

        // Numeric validations
        if (obj.CostPrice < 0)
            return ApiResponse.UnprocessableEntity("CostPrice phải lớn hơn hoặc bằng 0.", ApiCodeConstants.Common.InvalidData);
        if (obj.Weight < 0)
            return ApiResponse.UnprocessableEntity("Khối lượng (Weight) phải lớn hơn hoặc bằng 0.", ApiCodeConstants.Common.InvalidData);
        if (obj.MinStockLevel.HasValue && obj.MinStockLevel.Value < 0)
            return ApiResponse.UnprocessableEntity("MinStockLevel phải lớn hơn hoặc bằng 0.", ApiCodeConstants.Common.InvalidData);
        if (obj.IsIoTRequired && obj.Weight <= 0)
            return ApiResponse.UnprocessableEntity("Khi IsIoTRequired = true, Weight phải lớn hơn 0.", ApiCodeConstants.Common.InvalidData);

        // Validate AttributeValues (only overwrite when explicitly submitted)
        if (obj.AttributeValues != null)
        {
            var attrError = await ValidateAttributeValuesAsync(obj.AttributeValues);
            if (attrError != null)
                return ApiResponse.UnprocessableEntity(attrError, ApiCodeConstants.Common.InvalidData);
        }

        obj.ToEntity(existData);
        await _productVariantRepository.UpdateAsync(existData);
        await _productVariantRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    /// Kích hoạt biến thể sản phẩm (IsActive = true)
    public async Task<ApiResponse> ActivateAsync(int id, int updatedBy)
    {
        var existData = await _productVariantRepository.GetByIdAsync(id);
        if (existData == null || existData.IsDeleted)
            return ApiResponse.NotFound();

        existData.IsActive = true;
        existData.UpdatedBy = updatedBy;
        existData.LastModifiedDate = DateTime.Now;

        await _productVariantRepository.UpdateAsync(existData);
        await _productVariantRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    /// Vô hiệu hóa biến thể sản phẩm (IsActive = false)
    public async Task<ApiResponse> DeactivateAsync(int id, int updatedBy)
    {
        var existData = await _productVariantRepository.GetByIdAsync(id);
        if (existData == null || existData.IsDeleted)
            return ApiResponse.NotFound();

        existData.IsActive = false;
        existData.UpdatedBy = updatedBy;
        existData.LastModifiedDate = DateTime.Now;

        await _productVariantRepository.UpdateAsync(existData);
        await _productVariantRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateProductVariantDto> obj)
    {
        throw new NotImplementedException();
    }

    // ──────────────────────────────────────────────────────────
    // DELETE
    // ──────────────────────────────────────────────────────────
    /// Xóa mềm một biến thể sản phẩm theo ID — block nếu còn inventory/document references
    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var existData = await _productVariantRepository.GetByIdAsync(id);
        if (existData == null)
            return ApiResponse.NotFound();

        // Check inventory quantity
        var inventories = await _inventoryRepository
            .FindByCondition(x => x.ProductVariantId == id)
            .ToListAsync();

        if (inventories.Any(x => x.QuantityOnHand > 0))
            return ApiResponse.Conflict(
                "Không thể xóa biến thể khi còn tồn kho (QuantityOnHand > 0). Vui lòng vô hiệu hóa thay vì xóa.",
                ApiCodeConstants.Common.InvalidData);

        if (inventories.Any(x => x.QuantityReserved > 0))
            return ApiResponse.Conflict(
                "Không thể xóa biến thể khi còn số lượng đang đặt giữ (QuantityReserved > 0). Vui lòng vô hiệu hóa thay vì xóa.",
                ApiCodeConstants.Common.InvalidData);

        // Check inbound order references
        var hasInbound = await _inboundOrderItemRepository.AnyAsync(x => x.ProductVariantId == id && !x.IsDeleted);
        if (hasInbound)
            return ApiResponse.Conflict(
                "Biến thể đang được tham chiếu bởi phiếu nhập kho. Vui lòng vô hiệu hóa thay vì xóa.",
                ApiCodeConstants.Common.InvalidData);

        // Check outbound order references
        var hasOutbound = await _outboundOrderItemRepository.AnyAsync(x => x.ProductVariantId == id && !x.IsDeleted);
        if (hasOutbound)
            return ApiResponse.Conflict(
                "Biến thể đang được tham chiếu bởi phiếu xuất kho. Vui lòng vô hiệu hóa thay vì xóa.",
                ApiCodeConstants.Common.InvalidData);

        // Check stock take references
        var hasStockTake = await _stockTakeItemRepository.AnyAsync(x => x.ProductVariantId == id && !x.IsDeleted);
        if (hasStockTake)
            return ApiResponse.Conflict(
                "Biến thể đang được tham chiếu bởi phiếu kiểm kho. Vui lòng vô hiệu hóa thay vì xóa.",
                ApiCodeConstants.Common.InvalidData);

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

    // ──────────────────────────────────────────────────────────
    // QR CODE
    // ──────────────────────────────────────────────────────────
    /// Tạo và lưu QRCode cho biến thể sản phẩm (reuse QRCodeService)
    public async Task<ApiResponse> GenerateQrAsync(int id, int updatedBy)
    {
        var existData = await _productVariantRepository.GetByIdAsync(id);
        if (existData == null || existData.IsDeleted)
            return ApiResponse.NotFound();

        try
        {
            var qrUrl = await _qrCodeService.GenerateAndSaveQRUrlAsync(id);
            return ApiResponse.Success(new { QrUrl = qrUrl });
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse.UnprocessableEntity(ex.Message, ApiCodeConstants.Common.InvalidData);
        }
    }

    // ──────────────────────────────────────────────────────────
    // EXISTING: CheckSku / ConfirmScan
    // ──────────────────────────────────────────────────────────
    /// Kiểm tra SKU sản phẩm và tính toán số lượng tồn kho
    public async Task<ApiResponse> CheckSkuAsync(string sku, string? documentType = null, int? documentId = null)
    {
        var variant = await _productVariantRepository
            .FindByCondition(x => x.SKU.ToLower() == sku.ToLower() && !x.IsDeleted)
            .Include(x => x.Product).ThenInclude(p => p.ProductCategory)
            .Include(x => x.UnitOfMeasure)
            .Include(x => x.Image)
            .FirstOrDefaultAsync();

        if (variant == null)
            return ApiResponse.NotFound(message: $"Không tìm thấy biến thể sản phẩm với SKU '{sku}'.");

        var dto = await MapToDto(variant);

        var inventories = await _inventoryRepository
            .FindByCondition(x => x.ProductVariantId == variant.Id)
            .ToListAsync();

        decimal qtyOnHand = inventories.Sum(x => x.QuantityOnHand);
        decimal qtyReserved = inventories.Sum(x => x.QuantityReserved);
        decimal qtyAvailable = qtyOnHand - qtyReserved;

        var result = new SkuCheckResultDto
        {
            ProductVariant = dto,
            QuantityOnHand = qtyOnHand,
            QuantityReserved = qtyReserved,
            QuantityAvailable = qtyAvailable,
            BelongsToDocument = false,
            Message = "Tìm thấy sản phẩm. Chưa xác minh với phiếu."
        };

        if (!string.IsNullOrEmpty(documentType) && documentId.HasValue)
        {
            if (documentType.Equals("PurchaseOrder", StringComparison.OrdinalIgnoreCase) ||
                documentType.Equals("InboundOrder", StringComparison.OrdinalIgnoreCase))
            {
                var poItem = await _inboundOrderItemRepository
                    .FindByCondition(x => x.InboundOrderId == documentId.Value && x.ProductVariantId == variant.Id && !x.IsDeleted)
                    .FirstOrDefaultAsync();

                if (poItem != null)
                {
                    result.BelongsToDocument = true;
                    result.DocumentQuantityOrdered = poItem.QuantityOrdered;
                    result.DocumentQuantityProcessed = poItem.QuantityReceived;
                    result.IsQrScanned = poItem.QRScanned;
                    result.Message = "Sản phẩm hợp lệ và thuộc Phiếu mua hàng (Inbound Order).";
                }
                else result.Message = "Sản phẩm KHÔNG thuộc Phiếu mua hàng này.";
            }
            else if (documentType.Equals("SalesOrder", StringComparison.OrdinalIgnoreCase) ||
                     documentType.Equals("OutboundOrder", StringComparison.OrdinalIgnoreCase))
            {
                var soItem = await _outboundOrderItemRepository
                    .FindByCondition(x => x.OutboundOrderId == documentId.Value && x.ProductVariantId == variant.Id && !x.IsDeleted)
                    .FirstOrDefaultAsync();

                if (soItem != null)
                {
                    result.BelongsToDocument = true;
                    result.DocumentQuantityOrdered = soItem.QuantityOrdered;
                    result.DocumentQuantityProcessed = soItem.QuantityPicked;
                    result.IsQrScanned = soItem.QRScanned;
                    result.Message = "Sản phẩm hợp lệ và thuộc Đơn bán hàng (Outbound Order).";
                }
                else result.Message = "Sản phẩm KHÔNG thuộc Đơn bán hàng này.";
            }
            else if (documentType.Equals("StockTake", StringComparison.OrdinalIgnoreCase))
            {
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
                else result.Message = "Sản phẩm KHÔNG thuộc Phiếu kiểm kho này.";
            }
            else result.Message = $"Loại tài liệu '{documentType}' không được hỗ trợ.";
        }

        return ApiResponse.Success(result);
    }

    /// Xác nhận kết quả quét mã QR
    public async Task<ApiResponse> ConfirmScanAsync(ConfirmScanRequestDto request)
    {
        if (request == null)
            return ApiResponse.BadRequest(message: "Yêu cầu không hợp lệ.");

        var variant = await _productVariantRepository
            .FindByCondition(x => x.SKU.ToLower() == request.Sku.ToLower() && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (variant == null)
            return ApiResponse.NotFound(message: $"Không tìm thấy biến thể sản phẩm với SKU '{request.Sku}'.");

        if (request.DocumentType.Equals("PurchaseOrder", StringComparison.OrdinalIgnoreCase) ||
            request.DocumentType.Equals("InboundOrder", StringComparison.OrdinalIgnoreCase))
        {
            var poItem = await _inboundOrderItemRepository
                .FindByCondition(x => x.InboundOrderId == request.DocumentId && x.ProductVariantId == variant.Id && !x.IsDeleted)
                .FirstOrDefaultAsync();

            if (poItem == null)
                return ApiResponse.BadRequest(message: $"SKU '{request.Sku}' không thuộc Phiếu mua hàng ID {request.DocumentId}.");

            poItem.QRScanned = true;
            poItem.QuantityReceived += request.Quantity;
            poItem.LastModifiedDate = DateTime.Now;
            await _inboundOrderItemRepository.UpdateAsync(poItem);
            await _inboundOrderItemRepository.SaveChangesAsync();
            return ApiResponse.Success(message: "Xác nhận quét mã QR nhập kho thành công.");
        }
        else if (request.DocumentType.Equals("SalesOrder", StringComparison.OrdinalIgnoreCase) ||
                 request.DocumentType.Equals("OutboundOrder", StringComparison.OrdinalIgnoreCase))
        {
            var soItem = await _outboundOrderItemRepository
                .FindByCondition(x => x.OutboundOrderId == request.DocumentId && x.ProductVariantId == variant.Id && !x.IsDeleted)
                .FirstOrDefaultAsync();

            if (soItem == null)
                return ApiResponse.BadRequest(message: $"SKU '{request.Sku}' không thuộc Đơn bán hàng ID {request.DocumentId}.");

            soItem.QRScanned = true;
            soItem.QuantityPicked += request.Quantity;
            soItem.LastModifiedDate = DateTime.Now;
            await _outboundOrderItemRepository.UpdateAsync(soItem);
            await _outboundOrderItemRepository.SaveChangesAsync();
            return ApiResponse.Success(message: "Xác nhận quét mã QR xuất kho thành công.");
        }
        else if (request.DocumentType.Equals("StockTake", StringComparison.OrdinalIgnoreCase))
        {
            var stItem = await _stockTakeItemRepository
                .FindByCondition(x => x.StockTakeId == request.DocumentId && x.ProductVariantId == variant.Id && !x.IsDeleted)
                .FirstOrDefaultAsync();

            if (stItem == null)
                return ApiResponse.BadRequest(message: $"SKU '{request.Sku}' không thuộc Phiếu kiểm kho ID {request.DocumentId}.");

            stItem.QRScanned = true;
            stItem.ActualQuantity = (stItem.ActualQuantity ?? 0) + request.Quantity;
            stItem.LastModifiedDate = DateTime.Now;
            await _stockTakeItemRepository.UpdateAsync(stItem);
            await _stockTakeItemRepository.SaveChangesAsync();
            return ApiResponse.Success(message: "Xác nhận quét mã QR kiểm kho thành công.");
        }

        return ApiResponse.BadRequest(message: $"Loại tài liệu '{request.DocumentType}' không được hỗ trợ.");
    }
}
