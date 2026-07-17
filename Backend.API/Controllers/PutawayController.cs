using Asp.Versioning;
using Backend.Application.DTOs.Putaway;
using Backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers;

[Authorize]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/putaway")]
[ApiController]
public class PutawayController : BaseController
{
    private readonly IPutawaySuggestionService _putawaySuggestionService;

    public PutawayController(IPutawaySuggestionService putawaySuggestionService)
    {
        _putawaySuggestionService = putawaySuggestionService;
    }

    /// <summary>
    /// Lấy danh sách gợi ý vị trí lưu kho tối ưu cho lô hàng (Top 3-5).
    /// </summary>
    [HttpPost("suggestions")]
    public async Task<IActionResult> GetSuggestionsAsync([FromBody] GetPutawaySuggestionsRequest request)
    {
        var result = await _putawaySuggestionService.GetSuggestionsAsync(request, HttpContext.RequestAborted);
        return BaseResult(result);
    }

    /// <summary>
    /// Xem cấu hình trọng số gợi ý của một kho cụ thể hoặc cấu hình mặc định hệ thống.
    /// </summary>
    [HttpGet("config")]
    public async Task<IActionResult> GetConfigAsync([FromQuery] int? warehouseId)
    {
        var result = await _putawaySuggestionService.GetConfigAsync(warehouseId, HttpContext.RequestAborted);
        return BaseResult(result);
    }

    /// <summary>
    /// Cập nhật cấu hình trọng số quy tắc gợi ý vị trí.
    /// </summary>
    [HttpPut("config/{id}")]
    public async Task<IActionResult> UpdateConfigAsync(int id, [FromBody] UpdatePutawayRuleConfigDto dto)
    {
        var result = await _putawaySuggestionService.UpdateConfigAsync(id, dto, HttpContext.RequestAborted);
        return BaseResult(result);
    }
}
