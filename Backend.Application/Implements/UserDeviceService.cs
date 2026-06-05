using System;
using Backend.Application.DTOs.UserDevices;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;

namespace Backend.Application.Implements;

public class UserDeviceService : IUserDeviceService
{
    private readonly IUserDeviceRepository _userDeviceRepository;

    public UserDeviceService(IUserDeviceRepository userDeviceRepository)
    {
        _userDeviceRepository = userDeviceRepository;
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
