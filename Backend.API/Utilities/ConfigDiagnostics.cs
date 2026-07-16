using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Serilog;

namespace Backend.API.Utilities;

/// <summary>
/// In ra (đã che giá trị nhạy cảm) các nguồn cấu hình đã nạp và nguồn thực tế của từng key quan trọng,
/// giúp debug trên host KHÔNG có Shell (vd Render Free) — chỉ cần xem tab Logs.
///
/// Bật bằng biến môi trường CONFIG_DIAGNOSTICS=true (hoặc =1). Xem log xong nên tắt lại.
/// </summary>
public static class ConfigDiagnostics
{
    public static void LogEffectiveConfig(IConfiguration configuration)
    {
        var raw = configuration["CONFIG_DIAGNOSTICS"];
        var enabled = raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        if (!enabled) return;

        if (configuration is not IConfigurationRoot root) return;

        Log.Information("[CONFIG] ===== BẮT ĐẦU CHẨN ĐOÁN CẤU HÌNH =====");
        Log.Information("[CONFIG] Providers theo thứ tự nạp (nguồn SAU đè nguồn TRƯỚC): {Providers}",
            string.Join("  ->  ", root.Providers.Select(ProviderName)));
        Log.Information("[CONFIG] Secret file /etc/secrets/appsettings.json tồn tại: {Exists}",
            File.Exists("/etc/secrets/appsettings.json"));

        // Không nhạy cảm -> in đầy đủ để đối chiếu giá trị mới/cũ.
        foreach (var key in new[]
        {
            "ASPNETCORE_ENVIRONMENT",
            "HostSettings:AdminUrl",
            "HostSettings:ClientUrl",
            "FireBase:ServicePath",
            "Appsettings:ServicePath",
            "CorsOrigins:0",
            "CorsOrigins:1"
        })
            LogKey(root, key, mask: false);

        // Nhạy cảm -> che giá trị, chỉ để xem NGUỒN nào đang cấp.
        foreach (var key in new[]
        {
            "ConnectionStrings:DefaultConnectionString",
            "JwtSettings:SecretKey",
            "CloudinarySettings:ApiSecret",
            "SmtpSettings:Password",
            "HangfireSettings:ConnectionString"
        })
            LogKey(root, key, mask: true);

        // Kiểm tra file firebase có thật ở path đã cấu hình không (lỗi hay gặp khi lên container).
        var fbPath = configuration["FireBase:ServicePath"] ?? configuration["Appsettings:ServicePath"];
        if (!string.IsNullOrWhiteSpace(fbPath))
            Log.Information("[CONFIG] File firebase tại '{Path}' tồn tại: {Exists}", fbPath, File.Exists(fbPath));

        Log.Information("[CONFIG] ===== KẾT THÚC CHẨN ĐOÁN CẤU HÌNH =====");
    }

    private static void LogKey(IConfigurationRoot root, string key, bool mask)
    {
        var hits = new List<string>();
        foreach (var p in root.Providers)
        {
            if (p.TryGet(key, out var val))
                hits.Add($"{ProviderName(p)} = {(mask ? Mask(val) : val)}");
        }

        Log.Information("[CONFIG] {Key}  ==>  HIỆU LỰC: {Effective}  |  NGUỒN: {Sources}",
            key,
            mask ? Mask(root[key]) : (root[key] ?? "(null)"),
            hits.Count == 0 ? "(không nguồn nào có key này)" : string.Join("   ||   ", hits));
    }

    private static string ProviderName(IConfigurationProvider p)
        => p is JsonConfigurationProvider jp && jp.Source.Path != null
            ? $"Json({jp.Source.Path})"
            : p.GetType().Name;

    private static string Mask(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "(rỗng)";
        if (v.Length <= 8) return $"***(len={v.Length})";
        return $"{v[..4]}***{v[^3..]}(len={v.Length})";
    }
}
