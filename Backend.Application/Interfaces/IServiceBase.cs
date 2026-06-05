using System;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

/// <summary>
/// Interface service base
/// </summary>
/// <typeparam name="TKey">Type of id column</typeparam>
/// <typeparam name="CreateDto">Create DTO</typeparam>
/// <typeparam name="UpdateDto">Update DTO</typeparam>
/// <typeparam name="AdvancedDTParameter">Custom DTParameter</typeparam>
public interface IServiceBase<TKey, CreateDto, UpdateDto, AdvancedDTParameter>
    where CreateDto : class
    where UpdateDto : class
    where AdvancedDTParameter : DTParameter
{
    Task<ApiResponse> GetByIdAsync(TKey id);
    Task<ApiResponse> CreateAsync(CreateDto obj);
    Task<ApiResponse> CreateListAsync(IEnumerable<CreateDto> objs);
    Task<ApiResponse> UpdateAsync(UpdateDto obj);
    Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateDto> objs);
    Task<ApiResponse> SoftDeleteAsync(TKey id);
    Task<ApiResponse> SoftDeleteListAsync(IEnumerable<TKey> objs);
    Task<ApiResponse> GetAllAsync();
    Task<ApiResponse> GetPagedAsync(SearchQuery query);
    Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query);
    Task<ApiResponse> GetPagedAsync(AdvancedDTParameter parameters);
}
