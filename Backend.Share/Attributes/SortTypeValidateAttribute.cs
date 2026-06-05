using System;

namespace Backend.Share.Attributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public class SortTypeValidateAttribute : System.ComponentModel.DataAnnotations.ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string sortType) return false;

        if (sortType.ToLowerInvariant() != "asc" && sortType.ToLowerInvariant() != "desc") return false;

        return true;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} value must be ASC or DESC";
    }
}
