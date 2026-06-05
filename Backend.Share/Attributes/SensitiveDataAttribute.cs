using System;

namespace Backend.Share.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class SensitiveDataAttribute : Attribute
{
}
