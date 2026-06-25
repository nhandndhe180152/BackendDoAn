using System;

namespace Backend.Application.DTOs.UnitOfMeasures;

public class UnitOfMeasureDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}
