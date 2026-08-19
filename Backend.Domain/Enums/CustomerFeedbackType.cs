using System;

namespace Backend.Domain.Enums;

public static class CustomerFeedbackType
{
    public const string Quality = "QUALITY";
    public const string WrongProduct = "WRONG_PRODUCT";
    public const string Weight = "WEIGHT";
    public const string Packaging = "PACKAGING";
    public const string Delivery = "DELIVERY";
    public const string Other = "OTHER";

    public static readonly string[] All =
    {
        Quality, WrongProduct, Weight, Packaging, Delivery, Other
    };

    public static bool IsValid(string type)
    {
        if (string.IsNullOrWhiteSpace(type)) return false;
        return Array.Exists(All, t => t.Equals(type, StringComparison.OrdinalIgnoreCase));
    }
}
