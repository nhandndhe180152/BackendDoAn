using System.Threading;
using System.Threading.Tasks;
using Backend.Application.DTOs.CustomerFeedbacks;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers;

[Route("api/v1/customer-feedbacks")]
[ApiController]
[Authorize]
public class CustomerFeedbacksController : BaseController
{
    private readonly ICustomerFeedbackService _customerFeedbackService;

    public CustomerFeedbacksController(ICustomerFeedbackService customerFeedbackService)
    {
        _customerFeedbackService = customerFeedbackService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerFeedbackDto dto, CancellationToken cancellationToken)
    {
        var result = await _customerFeedbackService.CreateAsync(dto, cancellationToken);
        return BaseResult(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _customerFeedbackService.GetByIdAsync(id, cancellationToken);
        return BaseResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] DTParameter parameters, CancellationToken cancellationToken)
    {
        var result = await _customerFeedbackService.GetPagedAsync(parameters, cancellationToken);
        return BaseResult(result);
    }

    [HttpGet("sales-order/{salesOrderId}")]
    public async Task<IActionResult> GetBySalesOrder(int salesOrderId, CancellationToken cancellationToken)
    {
        var result = await _customerFeedbackService.GetBySalesOrderAsync(salesOrderId, cancellationToken);
        return BaseResult(result);
    }

    [HttpGet("outbound-order/{outboundOrderId}")]
    public async Task<IActionResult> GetByOutbound(int outboundOrderId, CancellationToken cancellationToken)
    {
        var result = await _customerFeedbackService.GetByOutboundAsync(outboundOrderId, cancellationToken);
        return BaseResult(result);
    }

    [HttpPut("{id}/resolve")]
    public async Task<IActionResult> Resolve(int id, [FromBody] ResolveCustomerFeedbackDto dto, CancellationToken cancellationToken)
    {
        var result = await _customerFeedbackService.ResolveAsync(id, dto, cancellationToken);
        return BaseResult(result);
    }

    [HttpGet("{id}/trace-investigation")]
    public async Task<IActionResult> GetTraceInvestigation(int id, CancellationToken cancellationToken)
    {
        var result = await _customerFeedbackService.GetTraceInvestigationAsync(id, cancellationToken);
        return BaseResult(result);
    }
}
