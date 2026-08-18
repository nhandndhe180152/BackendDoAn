using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Backend.Application.DTOs.ReturnToSuppliers;
using Backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers;

[ApiVersion(1)]
[Route("api/v{version:apiVersion}/return-to-suppliers")]
[Authorize]
[ApiController]
public class ReturnToSupplierOrderController : BaseController
{
    private readonly IReturnToSupplierOrderService _service;

    public ReturnToSupplierOrderController(IReturnToSupplierOrderService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateReturnToSupplierOrderDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(dto, cancellationToken);
        return BaseResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return BaseResult(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return BaseResult(result);
    }

    [HttpPut("{id}/approve")]
    public async Task<IActionResult> ApproveAsync(int id, [FromQuery] string? note, CancellationToken cancellationToken)
    {
        var result = await _service.ApproveAsync(id, note, cancellationToken);
        return BaseResult(result);
    }

    [HttpPut("{id}/confirm")]
    public async Task<IActionResult> ConfirmAsync(int id, CancellationToken cancellationToken)
    {
        var result = await _service.ConfirmAsync(id, cancellationToken);
        return BaseResult(result);
    }

    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> CancelAsync(int id, [FromQuery] string reason, CancellationToken cancellationToken)
    {
        var result = await _service.CancelAsync(id, reason, cancellationToken);
        return BaseResult(result);
    }
}
