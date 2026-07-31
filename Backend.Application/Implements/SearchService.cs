using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.DTOs.Search;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class SearchService : ISearchService
{
    private readonly IApplicationDbContext _context;

    public SearchService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse> GlobalSearchAsync(string? keyword, int limitPerGroup = 5)
    {
        var kw = (keyword ?? string.Empty).Trim();
        var groups = new List<GlobalSearchGroupDto>();

        // Từ khóa quá ngắn -> trả rỗng (tránh quét toàn bảng khi gõ 1 ký tự).
        if (kw.Length < 2)
        {
            return ApiResponse.Success(groups);
        }

        if (limitPerGroup <= 0) limitPerGroup = 5;
        if (limitPerGroup > 20) limitPerGroup = 20;

        // 1. Sản phẩm
        var products = await _context.Products
            .Where(x => !x.IsDeleted && x.Name.Contains(kw))
            .OrderBy(x => x.Name)
            .Take(limitPerGroup)
            .Select(x => new GlobalSearchItemDto
            {
                Id = x.Id,
                Title = x.Name,
                Subtitle = x.ProductCategory.Name
            })
            .ToListAsync();
        AddGroup(groups, "PRODUCT", "Sản phẩm", "/admin/products", products);

        // 2. Biến thể sản phẩm (theo tên hoặc SKU)
        var variants = await _context.ProductVariants
            .Where(x => !x.IsDeleted && (x.Name.Contains(kw) || x.SKU.Contains(kw)))
            .OrderBy(x => x.Name)
            .Take(limitPerGroup)
            .Select(x => new GlobalSearchItemDto
            {
                Id = x.Id,
                Title = x.Name,
                Subtitle = x.SKU
            })
            .ToListAsync();
        AddGroup(groups, "PRODUCT_VARIANT", "Biến thể sản phẩm", "/admin/product-variants", variants);

        // 3. Lô lúa/gạo (theo mã lô)
        var lots = await _context.PaddyLots
            .Where(x => !x.IsDeleted && x.LotCode.Contains(kw))
            .OrderByDescending(x => x.Id)
            .Take(limitPerGroup)
            .Select(x => new GlobalSearchItemDto
            {
                Id = x.Id,
                Title = x.LotCode,
                Subtitle = x.LotType
            })
            .ToListAsync();
        AddGroup(groups, "PADDY_LOT", "Lô lúa/gạo", "/admin/paddy-lots", lots);

        // 4. Đơn bán (theo mã SO, hoặc tên khách hàng)
        var salesOrders = await _context.SalesOrders
            .Where(x => !x.IsDeleted && (x.SOCode.Contains(kw) || x.Customer.Name.Contains(kw)))
            .OrderByDescending(x => x.Id)
            .Take(limitPerGroup)
            .Select(x => new GlobalSearchItemDto
            {
                Id = x.Id,
                Title = x.SOCode,
                Subtitle = x.Customer.Name
            })
            .ToListAsync();
        AddGroup(groups, "SALES_ORDER", "Đơn bán", "/admin/sales-orders", salesOrders);

        // 5. Đơn nhập kho (theo mã chứng từ, hoặc tên NCC)
        // Lấy field thô trước rồi dựng Title trong bộ nhớ (tránh nội suy chuỗi trong projection EF).
        var inboundRaw = await _context.InboundOrders
            .Where(x => !x.IsDeleted &&
                        ((x.POCode != null && x.POCode.Contains(kw)) ||
                         (x.Supplier != null && x.Supplier.Name.Contains(kw))))
            .OrderByDescending(x => x.Id)
            .Take(limitPerGroup)
            .Select(x => new
            {
                x.Id,
                x.POCode,
                SupplierName = x.Supplier != null ? x.Supplier.Name : null
            })
            .ToListAsync();
        var inboundOrders = inboundRaw.Select(x => new GlobalSearchItemDto
        {
            Id = x.Id,
            Title = string.IsNullOrEmpty(x.POCode) ? $"Phiếu nhập #{x.Id}" : x.POCode,
            Subtitle = x.SupplierName
        }).ToList();
        AddGroup(groups, "INBOUND_ORDER", "Đơn nhập kho", "/admin/inbound-orders", inboundOrders);

        // 6. Khách hàng (tên / mã / SĐT)
        var customers = await _context.Customers
            .Where(x => !x.IsDeleted &&
                        (x.Name.Contains(kw) || x.Code.Contains(kw) ||
                         (x.Phone != null && x.Phone.Contains(kw))))
            .OrderBy(x => x.Name)
            .Take(limitPerGroup)
            .Select(x => new GlobalSearchItemDto
            {
                Id = x.Id,
                Title = x.Name,
                Subtitle = x.Phone ?? x.Code
            })
            .ToListAsync();
        AddGroup(groups, "CUSTOMER", "Khách hàng", "/admin/customers", customers);

        // 7. Nông dân (tên / mã / SĐT)
        var farmers = await _context.Farmers
            .Where(x => !x.IsDeleted &&
                        (x.Name.Contains(kw) || x.Code.Contains(kw) ||
                         (x.Phone != null && x.Phone.Contains(kw))))
            .OrderBy(x => x.Name)
            .Take(limitPerGroup)
            .Select(x => new GlobalSearchItemDto
            {
                Id = x.Id,
                Title = x.Name,
                Subtitle = x.Phone ?? x.Code
            })
            .ToListAsync();
        AddGroup(groups, "FARMER", "Nông dân", "/admin/farmers", farmers);

        // 8. Nhà cung cấp (tên / mã / SĐT / email)
        var suppliers = await _context.Suppliers
            .Where(x => !x.IsDeleted &&
                        (x.Name.Contains(kw) || x.Code.Contains(kw) ||
                         (x.Phone != null && x.Phone.Contains(kw)) ||
                         (x.Email != null && x.Email.Contains(kw))))
            .OrderBy(x => x.Name)
            .Take(limitPerGroup)
            .Select(x => new GlobalSearchItemDto
            {
                Id = x.Id,
                Title = x.Name,
                Subtitle = x.Phone ?? x.Code
            })
            .ToListAsync();
        AddGroup(groups, "SUPPLIER", "Nhà cung cấp", "/admin/suppliers", suppliers);

        return ApiResponse.Success(groups);
    }

    private static void AddGroup(
        List<GlobalSearchGroupDto> groups,
        string type,
        string label,
        string url,
        List<GlobalSearchItemDto> items)
    {
        if (items.Count == 0) return;
        groups.Add(new GlobalSearchGroupDto
        {
            Type = type,
            Label = label,
            Url = url,
            Items = items
        });
    }
}
