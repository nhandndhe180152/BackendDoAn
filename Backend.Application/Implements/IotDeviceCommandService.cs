using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.IotDeviceCommands;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class IotDeviceCommandService : IIotDeviceCommandService
{
    private readonly IRepositoryBase<IotDeviceCommand, int> _commandBaseRepository;
    private readonly IRepositoryBase<IotDevice, int> _deviceBaseRepository;
    private readonly IIotDeviceCommandRepository _commandRepository;

    public IotDeviceCommandService(
        IRepositoryBase<IotDeviceCommand, int> commandBaseRepository,
        IRepositoryBase<IotDevice, int> deviceBaseRepository,
        IIotDeviceCommandRepository commandRepository)
    {
        _commandBaseRepository = commandBaseRepository;
        _deviceBaseRepository = deviceBaseRepository;
        _commandRepository = commandRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateIotDeviceCommandDto obj)
    {
        var device = await _deviceBaseRepository.FirstOrDefaultAsync(x =>
            x.Id == obj.IotDeviceId &&
            x.IsActive &&
            !x.IsDeleted);

        if (device == null)
        {
            return ApiResponse.NotFound();
        }

        var commandType = obj.CommandType.Trim().ToUpperInvariant();
        if (!IotDeviceCommandConstants.AllowedCommandTypes.Contains(commandType))
        {
            return ApiResponse.BadRequest();
        }

        // Tránh tạo quá nhiều lệnh đang chờ cho cùng 1 thiết bị.
        var hasPendingCommand = await _commandBaseRepository.AnyAsync(x =>
            x.IoTDeviceId == obj.IotDeviceId &&
            x.Status == IotDeviceCommandConstants.Status.Pending &&
            (x.ExpiredAt == null || x.ExpiredAt > DateTime.Now));

        if (hasPendingCommand)
        {
            return ApiResponse.UnprocessableEntity("Thiết bị này đang có lệnh chờ xử lý. Hãy chờ ESP32 lấy lệnh hoặc hủy lệnh cũ trước.");
        }

        var commandCode = await GenerateCommandCodeAsync();
        var entity = obj.ToEntity(commandCode);

        await _commandBaseRepository.CreateAsync(entity);
        await _commandBaseRepository.SaveChangesAsync();

        return ApiResponse.Created(entity.Id, "Tạo lệnh thiết bị IoT thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateIotDeviceCommandDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _commandBaseRepository
            .FindByCondition(x => !x.IsDeleted, true, x => x.IotDevice)
            .OrderByDescending(x => x.CreatedDate)
            .Take(200)
            .Select(x => new IotDeviceCommandDetailDto
            {
                Id = x.Id,
                IotDeviceId = x.IoTDeviceId,
                DeviceCode = x.IotDevice.DeviceCode,
                DeviceName = x.IotDevice.DeviceName,
                WarehouseId = x.IotDevice.WarehouseId,
                CommandCode = x.CommandCode,
                CommandType = x.CommandType,
                Payload = x.Payload,
                Status = x.Status,
                RequestedByUserId = x.RequestedByUserId,
                RequestedAt = x.RequestedAt,
                PickedUpAt = x.PickedUpAt,
                ExecutedAt = x.ExecutedAt,
                ExpiredAt = x.ExpiredAt,
                ResultMessage = x.ResultMessage,
                RetryCount = x.RetryCount,
                CreatedDate = x.CreatedDate
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _commandRepository.GetDetailByIdAsync(id);
        if (data == null)
        {
            return ApiResponse.NotFound();
        }

        return ApiResponse.Success(data.ToDto());
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _commandRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateIotDeviceCommandDto obj)
    {
        var entity = await _commandBaseRepository.GetByIdAsync(obj.Id);
        if (entity == null)
        {
            return ApiResponse.NotFound();
        }

        if (entity.Status != IotDeviceCommandConstants.Status.Pending)
        {
            return ApiResponse.BadRequest();
        }

        var device = await _deviceBaseRepository.FirstOrDefaultAsync(x =>
            x.Id == obj.IotDeviceId &&
            x.IsActive &&
            !x.IsDeleted);

        if (device == null)
        {
            return ApiResponse.NotFound();
        }

        obj.ToEntity(entity);

        await _commandBaseRepository.UpdateAsync(entity);
        await _commandBaseRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateIotDeviceCommandDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var entity = await _commandBaseRepository.GetByIdAsync(id);
        if (entity == null)
        {
            return ApiResponse.NotFound();
        }

        if (entity.Status == IotDeviceCommandConstants.Status.PickedUp)
        {
            return ApiResponse.BadRequest();
        }

        var isDeleted = await _commandBaseRepository.SoftDeleteAsync(id);
        if (!isDeleted)
        {
            return ApiResponse.BadRequest();
        }

        await _commandBaseRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> CancelAsync(int id, CancelIotDeviceCommandDto dto)
    {
        var entity = await _commandBaseRepository.GetByIdAsync(id);
        if (entity == null)
        {
            return ApiResponse.NotFound();
        }

        if (entity.Status != IotDeviceCommandConstants.Status.Pending)
        {
            return ApiResponse.BadRequest();
        }

        entity.Status = IotDeviceCommandConstants.Status.Cancelled;
        entity.ResultMessage = string.IsNullOrWhiteSpace(dto.Reason) ? "Command cancelled by user." : dto.Reason.Trim();
        entity.UpdatedBy = dto.UpdatedBy;
        entity.LastModifiedDate = DateTime.Now;

        await _commandBaseRepository.UpdateAsync(entity);
        await _commandBaseRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> GetPendingCommandForDeviceAsync(string deviceCode, string? deviceKey)
    {
        var deviceValidation = await ValidateDeviceAsync(deviceCode, deviceKey);
        if (!deviceValidation.IsSucceeded)
        {
            return deviceValidation.Response;
        }

        var device = deviceValidation.Device!;
        var command = await _commandRepository.GetNextPendingCommandAsync(device.Id, true);

        if (command == null)
        {
            // Không có lệnh, vẫn cập nhật heartbeat để biết thiết bị còn sống.
            device.LastHeartbeat = DateTime.Now;
            device.IsOnline = true;
            device.LastModifiedDate = DateTime.Now;
            await _deviceBaseRepository.UpdateAsync(device);
            await _deviceBaseRepository.SaveChangesAsync();

            return ApiResponse.Success<object?>(null, "Không có lệnh chờ xử lý.");
        }

        var now = DateTime.Now;
        command.Status = IotDeviceCommandConstants.Status.PickedUp;
        command.PickedUpAt = now;
        command.RetryCount += 1;
        command.LastModifiedDate = now;

        device.LastHeartbeat = now;
        device.IsOnline = true;
        device.LastModifiedDate = now;

        await _commandBaseRepository.UpdateAsync(command);
        await _deviceBaseRepository.UpdateAsync(device);
        await _commandBaseRepository.SaveChangesAsync();

        return ApiResponse.Success(command.ToPendingDto(), "ESP32 đã lấy lệnh thành công.");
    }

    public async Task<ApiResponse> CompleteCommandFromDeviceAsync(int commandId, CompleteIotDeviceCommandDto dto, string? deviceKey)
    {
        var command = await _commandRepository.GetCommandWithDeviceAsync(commandId, true);
        if (command == null)
        {
            return ApiResponse.NotFound();
        }

        if (command.IotDevice == null || command.IotDevice.IsDeleted || !command.IotDevice.IsActive)
        {
            return ApiResponse.Unauthorized();
        }

        if (!IsValidDeviceKey(command.IotDevice, deviceKey))
        {
            return ApiResponse.Unauthorized();
        }

        if (command.Status != IotDeviceCommandConstants.Status.PickedUp &&
            command.Status != IotDeviceCommandConstants.Status.Pending)
        {
            return ApiResponse.BadRequest();
        }

        var now = DateTime.Now;
        command.Status = dto.Success
            ? IotDeviceCommandConstants.Status.Executed
            : IotDeviceCommandConstants.Status.Failed;
        command.ExecutedAt = now;
        command.ResultMessage = string.IsNullOrWhiteSpace(dto.ResultMessage)
            ? (dto.Success ? "Command executed successfully." : "Command execution failed.")
            : dto.ResultMessage.Trim();
        command.LastModifiedDate = now;

        command.IotDevice.LastHeartbeat = now;
        command.IotDevice.IsOnline = true;
        command.IotDevice.LastModifiedDate = now;

        await _commandBaseRepository.UpdateAsync(command);
        await _deviceBaseRepository.UpdateAsync(command.IotDevice);
        await _commandBaseRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> MarkExpiredCommandsAsync()
    {
        var now = DateTime.Now;
        var expiredCommands = await _commandBaseRepository
            .FindByCondition(x =>
                x.Status == IotDeviceCommandConstants.Status.Pending &&
                x.ExpiredAt != null &&
                x.ExpiredAt <= now)
            .ToListAsync();

        if (!expiredCommands.Any())
        {
            return ApiResponse.Success(0, "Không có lệnh hết hạn.");
        }

        foreach (var command in expiredCommands)
        {
            command.Status = IotDeviceCommandConstants.Status.Expired;
            command.ResultMessage = "Command expired before device picked it up.";
            command.LastModifiedDate = now;
        }

        await _commandBaseRepository.UpdateListAsync(expiredCommands);
        await _commandBaseRepository.SaveChangesAsync();

        return ApiResponse.Success(expiredCommands.Count, "Đã cập nhật lệnh hết hạn.");
    }

    private async Task<string> GenerateCommandCodeAsync()
    {
        var prefix = $"CMD-{DateTime.Now:yyyyMMdd}";
        var countToday = await _commandBaseRepository.CountByConditionAsync(x =>
            x.CommandCode.StartsWith(prefix));

        return $"{prefix}-{countToday + 1:000}";
    }

    private async Task<(bool IsSucceeded, ApiResponse Response, IotDevice? Device)> ValidateDeviceAsync(string deviceCode, string? deviceKey)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            return (false, ApiResponse.BadRequest(), null);
        }

        var normalizedDeviceCode = deviceCode.Trim().ToUpperInvariant();

        var device = await _deviceBaseRepository.FirstOrDefaultAsync(x =>
            x.DeviceCode == normalizedDeviceCode &&
            x.IsActive &&
            !x.IsDeleted);

        if (device == null)
        {
            return (false, ApiResponse.Unauthorized(), null);
        }

        if (!IsValidDeviceKey(device, deviceKey))
        {
            return (false, ApiResponse.Unauthorized(), null);
        }

        return (true, ApiResponse.Success(), device);
    }

    private static bool IsValidDeviceKey(IotDevice device, string? deviceKey)
    {
        if (string.IsNullOrWhiteSpace(device.ApiKeyHash))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(deviceKey))
        {
            return false;
        }

        return DeviceKeyHelper.VerifyKey(deviceKey.Trim(), device.ApiKeyHash);
    }
}
