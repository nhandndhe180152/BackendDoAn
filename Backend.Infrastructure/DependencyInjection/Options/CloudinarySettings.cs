using System;

namespace Backend.Infrastructure.DependencyInjection.Options;

public class CloudinarySettings
{
    public string CloudName { get; set; } = null!;
    public string ApiKey { get; set; } = null!;
    public string ApiSecret { get; set; } = null!;
}
