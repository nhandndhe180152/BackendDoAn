using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.Constants;
using Backend.Application.DTOs.MillingOrders;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend.Domain.Enums;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Controller quản lý Lệnh xay xát (MillingOrder).
    /// POST /{id}/complete — hoàn thành lệnh xay, sinh lô gạo/phụ phẩm.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/milling-orders")]
    [Authorize]
    [ApiController]
    public class MillingOrderController : BaseController
    {
        private readonly IMillingOrderService _millingOrderService;

        public MillingOrderController(IMillingOrderService millingOrderService)
        {
            _millingOrderService = millingOrderService;
        }

        private bool HasAnyRole(params int[] allowedRoleIds)
        {
            var roleIds = this.GetLoggedInRoleIds();
            return allowedRoleIds.Any(roleIds.Contains);
        }

        private bool CanManageMillingOrder()
            => HasAnyRole(
                CommonConstants.Role.ADMIN,
                CommonConstants.Role.OWNER,
                CommonConstants.Role.MILLING);

        private bool CanHandleMillingInventory()
            => HasAnyRole(
                CommonConstants.Role.ADMIN,
                CommonConstants.Role.OWNER,
                CommonConstants.Role.MILLING,
                CommonConstants.Role.WAREHOUSE);

        private bool IsAdminOrOwner()
            => HasAnyRole(
                CommonConstants.Role.ADMIN,
                CommonConstants.Role.OWNER);

        [HttpGet]
        [CustomAuthorize(Enums.Menu.MILLING_ORDERS, Enums.Action.READ)]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _millingOrderService.GetAllAsync();
            return BaseResult(result);
        }

        /// <summary>Gap 2: Danh sách lệnh xay gắn với một đơn bán (điều phối xay-theo-đơn).</summary>
        [HttpGet("by-sales-order/{salesOrderId}")]
        [CustomAuthorize(Enums.Menu.MILLING_ORDERS, Enums.Action.READ)]
        public async Task<IActionResult> GetBySalesOrderAsync(int salesOrderId)
        {
            var result = await _millingOrderService.GetBySalesOrderAsync(salesOrderId);
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.MILLING_ORDERS, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _millingOrderService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.MILLING_ORDERS, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _millingOrderService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.MILLING_ORDERS, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateMillingOrderDto dto)
        {
            if (!CanManageMillingOrder())
            {
                return BaseResult(ApiResponse.Forbidden(
                    "Bạn không có quyền tạo lệnh xay xát.",
                    ApiCodeConstants.Common.Forbidden));
            }

            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _millingOrderService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.MILLING_ORDERS, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateMillingOrderDto dto)
        {
            if (!CanManageMillingOrder())
            {
                return BaseResult(ApiResponse.Forbidden(
                    "Bạn không có quyền chỉnh sửa lệnh xay xát.",
                    ApiCodeConstants.Common.Forbidden));
            }

            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _millingOrderService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpPost("{id}/reserve")]
        [CustomAuthorize(Enums.Menu.MILLING_ORDERS, Enums.Action.UPDATE)]
        public async Task<IActionResult> ReserveAsync(int id, [FromBody] ReserveMillingOrderDto dto)
        {
            if (!CanHandleMillingInventory())
            {
                return BaseResult(ApiResponse.Forbidden(
                    "Bạn không có quyền giữ lúa cho lệnh xay.",
                    ApiCodeConstants.Common.Forbidden));
            }

            var userId = this.GetLoggedInUserId();
            var result = await _millingOrderService.ReserveAsync(id, dto, userId);
            return BaseResult(result);
        }

        [HttpPost("{id}/start")]
        [CustomAuthorize(Enums.Menu.MILLING_ORDERS, Enums.Action.UPDATE)]
        public async Task<IActionResult> StartAsync(int id)
        {
            if (!CanManageMillingOrder())
            {
                return BaseResult(ApiResponse.Forbidden(
                    "Bạn không có quyền bắt đầu lệnh xay.",
                    ApiCodeConstants.Common.Forbidden));
            }

            var userId = this.GetLoggedInUserId();
            var result = await _millingOrderService.StartAsync(id, userId);
            return BaseResult(result);
        }

        [HttpPost("{id}/complete")]
        [CustomAuthorize(Enums.Menu.MILLING_ORDERS, Enums.Action.UPDATE)]
        public async Task<IActionResult> CompleteAsync(int id, [FromBody] CompleteMillingOrderDto dto)
        {
            if (!CanHandleMillingInventory())
            {
                return BaseResult(ApiResponse.Forbidden(
                    "Bạn không có quyền hoàn tất và nhập kho kết quả xay.",
                    ApiCodeConstants.Common.Forbidden));
            }

            var userId = this.GetLoggedInUserId();
            var result = await _millingOrderService.CompleteMillingOrderAsync(id, dto, userId);
            return BaseResult(result);
        }

        [HttpPost("{id}/cancel")]
        [CustomAuthorize(Enums.Menu.MILLING_ORDERS, Enums.Action.UPDATE)]
        public async Task<IActionResult> CancelAsync(int id)
        {
            if (!CanManageMillingOrder())
            {
                return BaseResult(ApiResponse.Forbidden(
                    "Bạn không có quyền hủy lệnh xay.",
                    ApiCodeConstants.Common.Forbidden));
            }

            var userId = this.GetLoggedInUserId();
            var result = await _millingOrderService.CancelAsync(id, userId);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.MILLING_ORDERS, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            if (!IsAdminOrOwner())
            {
                return BaseResult(ApiResponse.Forbidden(
                    "Chỉ Admin hoặc Chủ cơ sở mới có quyền xóa lệnh xay.",
                    ApiCodeConstants.Common.Forbidden));
            }

            var result = await _millingOrderService.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
