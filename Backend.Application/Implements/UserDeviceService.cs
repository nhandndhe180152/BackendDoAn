using System;
using Backend.Application.DTOs.UserDevices;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class UserDeviceService : IUserDeviceService
{
    private readonly IUserDeviceRepository _userDeviceRepository;
    private readonly IUserSessionRepository _userSessionRepository;

    public UserDeviceService(IUserDeviceRepository userDeviceRepository, IUserSessionRepository userSessionRepository)
    {
        _userDeviceRepository = userDeviceRepository;
        _userSessionRepository = userSessionRepository;
    }

    /// <summary>
    /// Đăng ký hoặc cập nhật thiết bị theo (UserId, DeviceId). Nếu có RefreshToken thì liên kết phiên
    /// hiện tại với thiết bị (đặt UserSession.UserDeviceId) để phục vụ đăng xuất theo thiết bị.
    /// </summary>
    public async Task<ApiResponse> RegisterDeviceAsync(RegisterDeviceDto dto)
    {
        try
        {
            if (dto.UserId == null || string.IsNullOrWhiteSpace(dto.DeviceId))
                return ApiResponse.BadRequest();

            var device = await _userDeviceRepository.FirstOrDefaultAsync(
                x => x.UserId == dto.UserId && x.DeviceId == dto.DeviceId && !x.IsDeleted);

            if (device == null)
            {
                device = dto.ToEntity();
                await _userDeviceRepository.CreateAsync(device);
            }
            else
            {
                device.DeviceName = dto.DeviceName ?? device.DeviceName;
                device.Platform = dto.Platform ?? device.Platform;
                device.OsVersion = dto.OsVersion ?? device.OsVersion;
                device.AppVersion = dto.AppVersion ?? device.AppVersion;
                device.UserAgent = dto.UserAgent ?? device.UserAgent;
                if (!string.IsNullOrWhiteSpace(dto.DeviceToken))
                    device.DeviceToken = dto.DeviceToken;
                device.LastModifiedDate = DateTime.Now;
                await _userDeviceRepository.UpdateAsync(device);
            }
            await _userDeviceRepository.SaveChangesAsync();

            // Liên kết phiên hiện tại (theo refresh token) với thiết bị.
            if (!string.IsNullOrWhiteSpace(dto.RefreshToken))
            {
                var session = await _userSessionRepository.FirstOrDefaultAsync(
                    x => x.UserId == dto.UserId && x.RefreshToken == dto.RefreshToken && !x.IsRevoked);
                if (session != null && session.UserDeviceId != device.Id)
                {
                    session.UserDeviceId = device.Id;
                    await _userSessionRepository.UpdateAsync(session);
                    await _userSessionRepository.SaveChangesAsync();
                }
            }

            return ApiResponse.Success(device.Id);
        }
        catch (Exception)
        {
            return ApiResponse.InternalServerError();
        }
    }

    public async Task<ApiResponse> GetMyDevicesAsync(int userId)
    {
        var devices = await _userDeviceRepository
            .FindByCondition(x => x.UserId == userId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new MyDeviceDto
            {
                Id = x.Id,
                DeviceId = x.DeviceId,
                DeviceName = x.DeviceName,
                Platform = x.Platform,
                OsVersion = x.OsVersion,
                AppVersion = x.AppVersion,
                UserAgent = x.UserAgent,
                HasActiveSession = x.UserSessions.Any(s => !s.IsRevoked && !s.IsUsed && s.ExpirationDate > DateTime.Now),
                CreatedDate = x.CreatedDate,
                LastModifiedDate = x.LastModifiedDate,
            })
            .ToListAsync();

        return ApiResponse.Success(devices);
    }

    public async Task<ApiResponse> LogoutDeviceAsync(int userId, string deviceId)
    {
        var device = await _userDeviceRepository.FirstOrDefaultAsync(
            x => x.UserId == userId && x.DeviceId == deviceId && !x.IsDeleted);
        if (device == null)
            return ApiResponse.NotFound();

        // Thu hồi các phiên gắn với thiết bị này.
        var sessions = await _userSessionRepository.FindByConditionAsync(
            x => x.UserId == userId && x.UserDeviceId == device.Id && !x.IsRevoked);
        if (sessions.Any())
        {
            foreach (var s in sessions) s.IsRevoked = true;
            await _userSessionRepository.UpdateListAsync(sessions);
        }

        // Xóa mềm đăng ký thiết bị -> biến mất khỏi danh sách "thiết bị đang đăng nhập".
        device.IsDeleted = true;
        device.LastModifiedDate = DateTime.Now;
        await _userDeviceRepository.UpdateAsync(device);

        await _userSessionRepository.SaveChangesAsync();
        await _userDeviceRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> LogoutOtherDevicesAsync(int userId, string currentDeviceId)
    {
        var currentDevice = await _userDeviceRepository.FirstOrDefaultAsync(
            x => x.UserId == userId && x.DeviceId == currentDeviceId && !x.IsDeleted);

        // Thu hồi mọi phiên KHÔNG thuộc thiết bị hiện tại (EF Core dùng ngữ nghĩa null của C#
        // nên != cũng bắt cả phiên chưa gắn thiết bị).
        IEnumerable<UserSession> sessions;
        if (currentDevice == null)
        {
            sessions = await _userSessionRepository.FindByConditionAsync(
                x => x.UserId == userId && !x.IsRevoked);
        }
        else
        {
            var currentId = currentDevice.Id;
            sessions = await _userSessionRepository.FindByConditionAsync(
                x => x.UserId == userId && !x.IsRevoked && x.UserDeviceId != currentId);
        }
        if (sessions.Any())
        {
            foreach (var s in sessions) s.IsRevoked = true;
            await _userSessionRepository.UpdateListAsync(sessions);
        }

        // Xóa mềm các thiết bị khác (giữ lại thiết bị hiện tại).
        List<UserDevice> otherDevices;
        if (currentDevice == null)
        {
            otherDevices = await _userDeviceRepository
                .FindByCondition(x => x.UserId == userId && !x.IsDeleted)
                .ToListAsync();
        }
        else
        {
            var currentRowId = currentDevice.Id;
            otherDevices = await _userDeviceRepository
                .FindByCondition(x => x.UserId == userId && !x.IsDeleted && x.Id != currentRowId)
                .ToListAsync();
        }
        if (otherDevices.Any())
        {
            foreach (var d in otherDevices) { d.IsDeleted = true; d.LastModifiedDate = DateTime.Now; }
            await _userDeviceRepository.UpdateListAsync(otherDevices);
        }

        await _userSessionRepository.SaveChangesAsync();
        await _userDeviceRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public Task<ApiResponse> CreateAsync(CreateUserDeviceDto obj)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateUserDeviceDto> objs)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetAllAsync()
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetByIdAsync(int id)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _userDeviceRepository.GetPagedAsync(parameters);

        return ApiResponse.Success(data);
    }

    public Task<ApiResponse> SoftDeleteAsync(int id)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> UpdateAsync(UpdateUserDeviceDto obj)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateUserDeviceDto> obj)
    {
        throw new NotImplementedException();
    }


    public async Task<ApiResponse> AddDeviceToken(CreateUserDeviceDto dto)
    {
        try
        {
            var oldDeviceToken = await _userDeviceRepository.FirstOrDefaultAsync(x => x.UserId == dto.UserId && x.DeviceToken == dto.DeviceToken && !x.IsDeleted);
            if (oldDeviceToken == null)
            {
                //Chưa có thì tạo mới
                var model = dto.ToEntity();
                await _userDeviceRepository.CreateAsync(model);
            }
            else
            {
                //Có rồi thì cập nhật isDelete = 0 để đỡ mất bản ghi
                oldDeviceToken.IsDeleted = false;
                await _userDeviceRepository.UpdateAsync(oldDeviceToken);
            }
            await _userDeviceRepository.SaveChangesAsync();

            return ApiResponse.Success();
        }
        catch (Exception)
        {
            return ApiResponse.InternalServerError();
        }
    }

    public async Task<ApiResponse> DeleteDeviceToken(DeleteUserDeviceDto dto)
    {
        try
        {
            var existingDevice = await _userDeviceRepository.FirstOrDefaultAsync(
                x => x.UserId == dto.UserId && x.DeviceToken == dto.DeviceToken && !x.IsDeleted
            );
            if (existingDevice != null)
            {
                existingDevice.IsDeleted = true;
                await _userDeviceRepository.UpdateAsync(existingDevice);
                await _userDeviceRepository.SaveChangesAsync();
            }
            return ApiResponse.Success();
        }
        catch (Exception)
        {
            return ApiResponse.InternalServerError();
        }
    }
}
