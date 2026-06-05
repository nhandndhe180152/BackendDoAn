using System;
using Backend.Application.DTOs.PaymentTransactions;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.DTParameters;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Http;

namespace Backend.Application.Implements;

public class PaymentTransactionService : IPaymentTransactionService
{
    private readonly IPaymentTransactionRepository _paymentTransactionRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PaymentTransactionService(IPaymentTransactionRepository paymentTransactionRepository, IHttpContextAccessor httpContextAccessor)
    {
        _paymentTransactionRepository = paymentTransactionRepository;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ApiResponse> CreateAsync(CreatePaymentTransactionDto obj)
    {
        var model = obj.ToEntity();

        await _paymentTransactionRepository.CreateAsync(model);
        await _paymentTransactionRepository.SaveChangesAsync();

        return ApiResponse.Success(model.Id);
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreatePaymentTransactionDto> objs)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetAllAsync()
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _paymentTransactionRepository.GetByIdAsync(id);
        if (data == null)
            return ApiResponse.NotFound();

        var dto = data.ToDto();

        return ApiResponse.Success(dto);

    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync(PaymentTransactionDTParameters parameters)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> SoftDeleteAsync(int id)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> UpdateAsync(UpdatePaymentTransactionDto obj)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdatePaymentTransactionDto> objs)
    {
        throw new NotImplementedException();
    }
}
