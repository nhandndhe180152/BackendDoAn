using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Customers;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Backend.Domain.Enums;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Controller quản lý danh mục Khách hàng (Customer) - CRUD + tìm kiếm/lọc/sắp xếp.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/customers")]
    [Authorize]
    [ApiController]
    public class CustomerController : BaseController
    {
        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [HttpGet]
        [CustomAuthorize(Enums.Menu.CUSTOMERS, Enums.Action.READ)]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _customerService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.CUSTOMERS, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _customerService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.CUSTOMERS, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _customerService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.CUSTOMERS, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateCustomerDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _customerService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.CUSTOMERS, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateCustomerDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _customerService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.CUSTOMERS, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _customerService.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
