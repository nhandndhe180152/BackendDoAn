using System;
using Backend.Application.DTOs.UserVerificationTokens;
using Backend.Application.Interfaces;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;

namespace Backend.Application.Implements;

public class UserVerificationTokenService : IUserVerificationTokenService
{
    private readonly IUserVerificationTokenRepository _userVerificationTokenRepository;

    public UserVerificationTokenService(IUserVerificationTokenRepository userVerificationTokenRepository)
    {
        _userVerificationTokenRepository = userVerificationTokenRepository;
    }

    public Task<ApiResponse> CreateAsync(CreateUserVerificationTokenDto obj)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateUserVerificationTokenDto> objs)
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
        var data = await _userVerificationTokenRepository.GetPagedAsync(parameters);

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

    public Task<ApiResponse> UpdateAsync(UpdateUserVerificationTokenDto obj)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateUserVerificationTokenDto> obj)
    {
        throw new NotImplementedException();
    }
}
