using System;

namespace Backend.Infrastructure.DependencyInjection.Options;

public class JwtSettings
{
    public string SecretKey { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public int ExpireTime { get; set; }
    public int RefreshTokenTtl { get; set; }
}
