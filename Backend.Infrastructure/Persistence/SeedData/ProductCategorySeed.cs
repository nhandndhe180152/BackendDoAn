using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

/// <summary>
/// Seed danh mục sản phẩm lúa/gạo/phụ phẩm.
/// Sau khi pivot nghiệp vụ sang lot-centric, ProductCategory chỉ còn dùng để
/// phân nhóm hiển thị tồn kho và lọc inventory. Ba danh mục cốt lõi phải có sẵn.
/// </summary>
public static class ProductCategorySeed
{
    public static IEnumerable<ProductCategory> GetCategories()
    {
        return new[]
        {
            new ProductCategory
            {
                Id = 101,
                Name = "Lúa thô",
                Description = "Lúa nguyên liệu đầu vào",
                ParentCategoryId = null,
                TreeIds = "101",
                SortOrder = 101,
                CreatedDate = new DateTime(2026, 1, 1)
            },
            new ProductCategory
            {
                Id = 102,
                Name = "Gạo thành phẩm",
                Description = "Gạo sau xay xát",
                ParentCategoryId = null,
                TreeIds = "102",
                SortOrder = 102,
                CreatedDate = new DateTime(2026, 1, 1)
            },
            new ProductCategory
            {
                Id = 103,
                Name = "Phụ phẩm",
                Description = "Tấm, cám, trấu từ quá trình xay xát",
                ParentCategoryId = null,
                TreeIds = "103",
                SortOrder = 103,
                CreatedDate = new DateTime(2026, 1, 1)
            },
        };
    }
}
