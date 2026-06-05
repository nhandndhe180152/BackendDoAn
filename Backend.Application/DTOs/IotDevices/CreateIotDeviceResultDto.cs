using System;

namespace Backend.Application.DTOs.IotDevices;

public class CreateIotDeviceResultDto
{
    public int Id { get; set; }

    public string DeviceCode { get; set; } = string.Empty;

    /// <summary>
    /// Plain device key trả về cho người dùng cấu hình vào ESP32.
    /// Backend chỉ lưu hash, không lưu plain key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    public string HeaderName { get; set; } = "X-Device-Key";

    public string Note { get; set; } = "Hãy lưu Device Key này để cấu hình vào ESP32. Hệ thống sẽ không hiển thị lại key cũ.";
}
