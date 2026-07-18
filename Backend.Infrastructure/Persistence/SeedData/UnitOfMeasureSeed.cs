using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

/// <summary>
/// Seed đơn vị tính cơ bản.
/// UoM Id=1 (Kilogram) là bắt buộc vì ProductVariant lúa/gạo đều dùng kg.
/// </summary>
public static class UnitOfMeasureSeed
{
    public static IEnumerable<UnitOfMeasure> GetUnits()
    {
        return new[]
        {
            new UnitOfMeasure { Id = 101, Name = "Kilogram",   Symbol = "kg",  CreatedDate = new DateTime(2026, 1, 1) },
            new UnitOfMeasure { Id = 102, Name = "Tấn",        Symbol = "T",   CreatedDate = new DateTime(2026, 1, 1) },
            new UnitOfMeasure { Id = 103, Name = "Bao (50kg)", Symbol = "bao", CreatedDate = new DateTime(2026, 1, 1) },
        };
    }
}
