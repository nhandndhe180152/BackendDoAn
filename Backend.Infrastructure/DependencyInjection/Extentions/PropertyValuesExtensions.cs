using System;
using Backend.Share.Attributes;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Backend.Infrastructure.DependencyInjection.Extentions;

public static class PropertyValuesExtensions
{
    public static Dictionary<string, object?> ToSafeDictionary(this PropertyValues values)
    {
        var entityType = values.EntityType.ClrType;
        var dict = new Dictionary<string, object?>();

        foreach (var prop in values.Properties)
        {
            var propertyInfo = entityType.GetProperty(prop.Name);
            var isSensitive = propertyInfo?.GetCustomAttributes(typeof(SensitiveDataAttribute), true).Any() ?? false;
            dict[prop.Name] = isSensitive ? "*****" : values[prop];
        }

        return dict;
    }
}
