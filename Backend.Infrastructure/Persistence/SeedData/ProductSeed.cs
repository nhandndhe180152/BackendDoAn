using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

/// <summary>
/// Seed sản phẩm đại diện cho 3 nhóm hàng hóa chính trong chuỗi lúa/gạo.
/// Product chỉ còn vai trò phân nhóm (CategoryId) sau khi pivot sang lot-centric.
/// </summary>
public static class ProductSeed
{
    public static IEnumerable<Product> GetProducts()
    {
        return new[]
        {
            new Product
            {
                Id = 101,
                Name = "Lúa thô",
                Description = "Lúa nguyên liệu đầu vào thu mua từ nông dân",
                ProductCategoryId = 101,
                IsActive = true,
                CreatedDate = new DateTime(2026, 1, 1)
            },
            new Product
            {
                Id = 102,
                Name = "Gạo",
                Description = "Gạo thành phẩm sau xay xát",
                ProductCategoryId = 102,
                IsActive = true,
                CreatedDate = new DateTime(2026, 1, 1)
            },
            new Product
            {
                Id = 103,
                Name = "Phụ phẩm",
                Description = "Tấm, cám, trấu sinh ra trong quá trình xay xát",
                ProductCategoryId = 103,
                IsActive = true,
                CreatedDate = new DateTime(2026, 1, 1)
            },
        };
    }
}
