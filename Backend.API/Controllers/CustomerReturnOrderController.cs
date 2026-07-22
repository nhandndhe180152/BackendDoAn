using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.CustomerReturns;
using Backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers;

[ApiVersion(1)]
[Route("api/v{version:apiVersion}/customer-returns")]
[Authorize]
[ApiController]
public class CustomerReturnOrderController : BaseController
{
    private readonly ICustomerReturnOrderService _returnOrderService;

    public CustomerReturnOrderController(ICustomerReturnOrderService returnOrderService)
    {
        _returnOrderService = returnOrderService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateCustomerReturnOrderDto dto)
    {
        var result = await _returnOrderService.CreateAsync(dto);
        return BaseResult(result);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateAsync([FromBody] UpdateCustomerReturnOrderDto dto)
    {
        var result = await _returnOrderService.UpdateAsync(dto);
        return BaseResult(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var result = await _returnOrderService.GetByIdAsync(id);
        return BaseResult(result);
    }

    [HttpPost("paged")]
    public async Task<IActionResult> GetPagedAsync([FromBody] CustomerReturnOrderPagedQuery query)
    {
        var result = await _returnOrderService.GetPagedAsync(query);
        return BaseResult(result);
    }

    [HttpPut("{id}/approve")]
    public async Task<IActionResult> ApproveAsync(int id, [FromQuery] string? note)
    {
        var result = await _returnOrderService.ApproveAsync(id, note);
        return BaseResult(result);
    }

    [HttpPut("inspect")]
    public async Task<IActionResult> InspectAsync([FromBody] InspectCustomerReturnOrderDto dto)
    {
        var result = await _returnOrderService.InspectAsync(dto);
        return BaseResult(result);
    }

    [HttpGet("{id}/impact-preview")]
    public async Task<IActionResult> GetImpactPreviewAsync(int id)
    {
        var result = await _returnOrderService.GetImpactPreviewAsync(id);
        return BaseResult(result);
    }

    [HttpPut("{id}/confirm")]
    public async Task<IActionResult> ConfirmAsync(int id)
    {
        var result = await _returnOrderService.ConfirmAsync(id);
        return BaseResult(result);
    }

    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> CancelAsync(int id, [FromQuery] string reason)
    {
        var result = await _returnOrderService.CancelAsync(id, reason);
        return BaseResult(result);
    }
}
