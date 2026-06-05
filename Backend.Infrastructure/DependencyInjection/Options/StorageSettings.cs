using System;

namespace Backend.Infrastructure.DependencyInjection.Options;

public class StorageSettings
{
    public string StorageUrl { get; set; } = null!;
    public string StorageIpAddress { get; set; } = null!;
    public int ExpireTime { get; set; }
    public string SecretKey { get; set; } = null!;
    public string StorageUploadApi { get; set; } = null!;
    public string StorageUploadMultipleApi { get; set; } = null!;
    public string StorageCreateFolderApi { get; set; } = null!;
    public string StorageTemporaryUrl { get; set; } = null!;
}
