using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Users;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/user")]
    [ApiController]
    [Authorize]
    public class UserController : BaseController, IBaseController<int, CreateUserDto, UpdateUserDto, UserDTParameters>
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateUserDto obj)
        {
            // Mật khẩu do hệ thống tự sinh trong UserService và gửi qua email; controller không xử lý mật khẩu.
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _userService.CreateAsync(obj);

            return BaseResult(result);
        }

        /// <summary>Tạo hàng loạt user (toàn bộ hoặc không). Trả lỗi theo từng dòng nếu có.</summary>
        [HttpPost("create-list")]
        public async Task<IActionResult> CreateListAsync([FromBody] List<CreateUserDto> objs)
        {
            var userId = this.GetLoggedInUserId();
            foreach (var o in objs)
                o.CreatedBy = userId;

            var result = await _userService.CreateListAsync(objs);

            return BaseResult(result);
        }

        /// <summary>Tải file mẫu để import tạo user hàng loạt (format = xlsx | csv).</summary>
        [HttpGet("import-template")]
        public async Task<IActionResult> ImportTemplateAsync([FromQuery] string format = "xlsx")
        {
            var (content, contentType, fileName) = await _userService.GenerateImportTemplateAsync(format);
            return File(content, contentType, fileName);
        }

        /// <summary>Đọc file Excel/CSV upload, trả về danh sách dòng user để hiển thị/kiểm tra trước khi tạo.</summary>
        [HttpPost("import-parse")]
        public async Task<IActionResult> ImportParseAsync([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BaseResult(ApiResponse.BadRequest("Vui lòng chọn file."));

            using var stream = file.OpenReadStream();
            var result = await _userService.ParseImportFileAsync(stream, file.FileName);

            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _userService.GetAllAsync();

            return BaseResult(result);
        }

        [HttpPost("all")]
        public async Task<IActionResult> GetAllAsync([FromBody] UserSearchQuery query)
        {
            var result = await _userService.GetAllAsync(query);

            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _userService.GetByIdAsync(id);

            return BaseResult(data);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _userService.GetPagedAsync(query);

            return BaseResult(data);
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] UserDTParameters parameters)
        {
            var data = await _userService.GetPagedAsync(parameters);

            return BaseResult(data);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var data = await _userService.SoftDeleteAsync(id);

            return BaseResult(data);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateUserDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var data = await _userService.UpdateAsync(obj);

            return BaseResult(data);
        }

        [HttpGet("menus")]
        public async Task<IActionResult> GetMenuAsync()
        {
            var userId = this.GetLoggedInUserId();
            var result = await _userService.GetMenuAsync(userId);

            return BaseResult(result);
        }

        [HttpGet("permissions")]
        public async Task<IActionResult> GetPermissionsAsync()
        {
            var userId = this.GetLoggedInUserId();
            var result = await _userService.GetPermissionsAsync(userId);

            return BaseResult(result);
        }

        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatisticsAsync()
        {
            var result = await _userService.GetStatisticsAsync();

            return BaseResult(result);
        }

        [HttpGet("me")]
        public async Task<IActionResult> Profile()
        {
            var userId = this.GetLoggedInUserId();
            var result = await _userService.GetProfileAsync(userId);

            return BaseResult(result);
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateProfileAsync([FromBody] UpdateUserProfileDto obj)
        {
            int userId = this.GetLoggedInUserId();
            var data = await _userService.UpdateProfileAsync(userId, obj);

            return BaseResult(data);
        }

        [HttpPut("me/change-password")]
        public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordDto changePasswordDto)
        {
            int userId = this.GetLoggedInUserId();
            var data = await _userService.ChangePasswordAsync(userId, changePasswordDto);
            return BaseResult(data);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchUser([FromQuery] UserSearchQuery query)
        {
            var data = await _userService.GetPagedAsync(query);

            return BaseResult(data);
        }

        [HttpGet("paged-end-user")]
        public async Task<IActionResult> GetPagedEndUserAsync([FromQuery] SearchQuery query)
        {
            var data = await _userService.GetPagedEndUserAsync(query);
            return BaseResult(data);
        }

        [HttpDelete("me")]
        public async Task<IActionResult> Deactivate()
        {
            var userId = this.GetLoggedInUserId();
            var result = await _userService.Deactivate(userId);

            return BaseResult(result);
        }

    }
}
