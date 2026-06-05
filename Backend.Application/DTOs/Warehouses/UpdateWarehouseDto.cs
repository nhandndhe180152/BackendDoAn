using System;

namespace Backend.Application.DTOs.Warehouses;

public class UpdateWarehouseDto : CreateWarehouseDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
