using System;
using Backend.Application.DTOs.UserDevices;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class UserDeviceMapping
{
    public static UserDevice ToEntity(this CreateUserDeviceDto obj)
    {
        return new UserDevice
        {
            UserId = (int)obj.UserId,
            DeviceName = obj.DeviceName,
            Platform = obj.Platform,
            OsVersion = obj.OsVersion,
            AppVersion = obj.AppVersion,
            DeviceToken = obj.DeviceToken,
            UserAgent = obj.UserAgent,
            CreatedDate = DateTime.Now,
            CreatedBy = obj.UserId,
        };
    }
}
