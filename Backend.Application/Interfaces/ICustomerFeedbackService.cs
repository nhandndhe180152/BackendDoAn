using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.DTOs.CustomerFeedbacks;
using Backend.Domain.Abstractions;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface ICustomerFeedbackService
{
    Task<ApiResponse> CreateAsync(CreateCustomerFeedbackDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResponse> GetBySalesOrderAsync(int salesOrderId, CancellationToken cancellationToken = default);
    Task<ApiResponse> GetByOutboundAsync(int outboundOrderId, CancellationToken cancellationToken = default);
    Task<ApiResponse> ResolveAsync(int id, ResolveCustomerFeedbackDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse> GetTraceInvestigationAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResponse> GetPagedAsync(DTParameter parameters, CancellationToken cancellationToken = default);
}
