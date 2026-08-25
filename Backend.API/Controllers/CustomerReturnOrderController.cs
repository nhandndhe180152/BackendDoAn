using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.CustomerReturns;
using Backend.Application.Interfaces;
using Backend.Domain.Enums;
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
    [CustomAuthorize(Enums.Menu.CUSTOMER_RETURNS, Enums.Action.CREATE)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateCustomerReturnOrderDto dto)
    {
        var result = await _returnOrderService.CreateAsync(dto);
        return BaseResult(result);
    }

    [HttpPut]
    [CustomAuthorize(Enums.Menu.CUSTOMER_RETURNS, Enums.Action.UPDATE)]
    public async Task<IActionResult> UpdateAsync([FromBody] UpdateCustomerReturnOrderDto dto)
    {
        var result = await _returnOrderService.UpdateAsync(dto);
        return BaseResult(result);
    }

    [HttpGet("{id}")]
    [CustomAuthorize(Enums.Menu.CUSTOMER_RETURNS, Enums.Action.READ)]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var result = await _returnOrderService.GetByIdAsync(id);
        return BaseResult(result);
    }

    [HttpPost("paged")]
    [CustomAuthorize(Enums.Menu.CUSTOMER_RETURNS, Enums.Action.READ)]
    public async Task<IActionResult> GetPagedAsync([FromBody] CustomerReturnOrderPagedQuery query)
    {
        var result = await _returnOrderService.GetPagedAsync(query);
        return BaseResult(result);
    }

    [HttpGet]
    [CustomAuthorize(Enums.Menu.CUSTOMER_RETURNS, Enums.Action.READ)]
    public async Task<IActionResult> GetPagedByQueryAsync([FromQuery] CustomerReturnOrderPagedQuery query)
    {
        var result = await _returnOrderService.GetPagedAsync(query);
        return BaseResult(result);
    }

    [HttpPut("{id}/approve")]
    [CustomAuthorize(Enums.Menu.CUSTOMER_RETURNS, Enums.Action.APPROVE)]
    public async Task<IActionResult> ApproveAsync(int id, [FromQuery] string? note)
    {
        var result = await _returnOrderService.ApproveAsync(id, note);
        return BaseResult(result);
    }

    /// <summary>Danh sách phiếu xuất đã giao còn số lượng có thể trả.</summary>
    [HttpGet("sources")]
    [CustomAuthorize(Enums.Menu.CUSTOMER_RETURNS, Enums.Action.READ)]
    public async Task<IActionResult> GetReturnSourcesAsync([FromQuery] CustomerReturnSourceQuery query)
    {
        return BaseResult(await _returnOrderService.GetReturnSourcesAsync(query));
    }

    /// <summary>Khách hàng, kho và chi tiết lô được tự động lấy từ phiếu xuất gốc.</summary>
    [HttpGet("sources/{outboundOrderId:int}")]
    [CustomAuthorize(Enums.Menu.CUSTOMER_RETURNS, Enums.Action.READ)]
    public async Task<IActionResult> GetReturnSourceByIdAsync(int outboundOrderId)
    {
        return BaseResult(await _returnOrderService.GetReturnSourceByIdAsync(outboundOrderId));
    }

    [HttpPut("{id}/submit")]
    [CustomAuthorize(Enums.Menu.CUSTOMER_RETURNS, Enums.Action.UPDATE)]
    public async Task<IActionResult> SubmitAsync(int id)
    {
        return BaseResult(await _returnOrderService.SubmitAsync(id));
    }

    [HttpPut("{id}/reject")]
    [CustomAuthorize(Enums.Menu.CUSTOMER_RETURNS, Enums.Action.APPROVE)]
    public async Task<IActionResult> RejectAsync(int id, [FromQuery] string reason)
    {
        return BaseResult(await _returnOrderService.RejectAsync(id, reason));
    }

    [HttpPut("receive")]
    [CustomAuthorize(Enums.Menu.CUSTOMER_RETURNS, Enums.Action.UPDATE)]
    public async Task<IActionResult> ReceiveAsync([FromBody] ReceiveCustomerReturnOrderDto dto)
    {
        return BaseResult(await _returnOrderService.ReceiveAsync(dto));
    }

    [HttpPut("inspect")]
    [CustomAuthorize(Enums.Menu.CUSTOMER_RETURNS, Enums.Action.UPDATE)]
    public async Task<IActionResult> InspectAsync([FromBody] InspectCustomerReturnOrderDto dto)
    {
        var result = await _returnOrderService.InspectAsync(dto);
        return BaseResult(result);
    }

    [HttpGet("{id}/impact-preview")]
    [CustomAuthorize(Enums.Menu.CUSTOMER_RETURNS, Enums.Action.READ)]
    public async Task<IActionResult> GetImpactPreviewAsync(int id)
    {
        var result = await _returnOrderService.GetImpactPreviewAsync(id);
        return BaseResult(result);
    }

    [HttpPut("{id}/confirm")]
    [CustomAuthorize(Enums.Menu.CUSTOMER_RETURNS, Enums.Action.APPROVE)]
    public async Task<IActionResult> ConfirmAsync(int id)
    {
        var result = await _returnOrderService.ConfirmAsync(id);
        return BaseResult(result);
    }

    [HttpPost("{id}/refunds")]
    [CustomAuthorize(Enums.Menu.CUSTOMER_RETURNS, Enums.Action.APPROVE)]
    public async Task<IActionResult> RegisterRefundAsync(int id, [FromBody] RegisterCustomerReturnRefundDto dto)
    {
        return BaseResult(await _returnOrderService.RegisterRefundAsync(id, dto));
    }

    [HttpPut("{id}/cancel")]
    [CustomAuthorize(Enums.Menu.CUSTOMER_RETURNS, Enums.Action.APPROVE)]
    public async Task<IActionResult> CancelAsync(int id, [FromQuery] string reason)
    {
        var result = await _returnOrderService.CancelAsync(id, reason);
        return BaseResult(result);
    }
}
