using System;

namespace Backend.Application.DTOs.ProductVariants;

/// Represents one parsed attribute entry in ProductVariant.AttributeValues JSON
public class AttributeValueDto
{
    public int AttributeId { get; set; }
    public string? AttributeName { get; set; }
    public string Value { get; set; } = null!;
}
