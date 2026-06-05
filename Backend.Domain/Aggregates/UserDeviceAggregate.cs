using System;

namespace Backend.Domain.Aggregates;

public class UserDeviceAggregate
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = null!;
    public string? DeviceName { get; set; }
    public string? Platform { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public string? DeviceToken { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedDate { get; set; }
}
