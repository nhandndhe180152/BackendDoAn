using System;

namespace Backend.Application.DependencyInjection.Options;

public class HostSettings
{
    public string AdminUrl { get; set; } = null!;
    public string ClientUrl { get; set; } = null!;
    public string ApiUrl { get; set; } = null!;
}
