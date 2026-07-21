using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

/// <summary>
/// Seed ProductVariant đại diện cho lúa thô, gạo, và phụ phẩm.
///
/// Vai trò sau khi pivot sang lot-centric:
/// - Thỏa FK NOT NULL của Inventory.ProductVariantId và InventoryTransaction.ProductVariantId.
/// - Cung cấp Weight (kg/bao) để quy đổi số bao từ QoH (kg).
/// - RiceVarietyId = null → variant "chung" (fallback khi chưa cấu hình variant theo giống cụ thể).
///
/// Weight = 50 (kg/bao) là quy ước mặc định bao lúa 50kg.
/// SKU phải unique — dùng tiền tố PV- để tránh xung đột với SKU tự sinh.
/// </summary>
public static class ProductVariantSeed
{
    public static IEnumerable<ProductVariant> GetVariants()
    {
        return new[]
        {
            // Lúa thô — variant chung (fallback khi không khớp giống cụ thể)
            new ProductVariant
            {
                Id = 101,
                ProductId = 101,
                UnitOfMeasureId = 101,      // Kilogram
                Name = "Lúa thô (chung)",
                SKU = "PV-LUA-CHUNG",
                CostPrice = 0,
                SalePrice = 0,
                Weight = 50m,               // 50 kg/bao — quy ước mặc định
                IsActive = true,
                IsByproduct = false,
                RiceVarietyId = null,       // variant chung, không gắn giống cụ thể
                CreatedDate = new DateTime(2026, 1, 1)
            },
            // Gạo thành phẩm — variant chung
            new ProductVariant
            {
                Id = 102,
                ProductId = 102,
                UnitOfMeasureId = 101,
                Name = "Gạo (chung)",
                SKU = "PV-GAO-CHUNG",
                CostPrice = 0,
                SalePrice = 0,
                Weight = 50m,
                IsActive = true,
                IsByproduct = false,
                RiceVarietyId = null,
                CreatedDate = new DateTime(2026, 1, 1)
            },
            // Phụ phẩm — variant chung (tấm/cám/trấu)
            new ProductVariant
            {
                Id = 103,
                ProductId = 103,
                UnitOfMeasureId = 101,
                Name = "Phụ phẩm (chung)",
                SKU = "PV-PHUPHAM-CHUNG",
                CostPrice = 0,
                SalePrice = 0,
                Weight = 50m,
                IsActive = true,
                IsByproduct = true,         // Đánh dấu phụ phẩm
                RiceVarietyId = null,
                CreatedDate = new DateTime(2026, 1, 1)
            },
        };
    }
}
