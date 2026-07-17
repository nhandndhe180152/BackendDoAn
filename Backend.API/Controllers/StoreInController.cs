using Asp.Versioning;
using Backend.Application.DTOs.Putaway;
using Backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers;

[Authorize]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/store-in")]
[ApiController]
public class StoreInController : BaseController
{
    private readonly IPutawaySuggestionService _putawaySuggestionService;

    public StoreInController(IPutawaySuggestionService putawaySuggestionService)
    {
        _putawaySuggestionService = putawaySuggestionService;
    }

    /// <summary>
    /// Xác nhận thực tế xếp hàng vào ô kệ/vị trí lưu kho.
    /// Thực hiện đồng thời trong 1 transaction an toàn, kiểm tra tranh chấp chỗ và tăng tồn kho vật lý.
    /// </summary>
    [HttpPost("{referenceType}/{referenceId}/confirm")]
    public async Task<IActionResult> ConfirmStoreInAsync(
        string referenceType,
        int referenceId,
        [FromBody] ConfirmStoreInRequest request)
    {
        var result = await _putawaySuggestionService.ConfirmStoreInAsync(referenceType, referenceId, request, HttpContext.RequestAborted);
        return BaseResult(result);
    }
}
