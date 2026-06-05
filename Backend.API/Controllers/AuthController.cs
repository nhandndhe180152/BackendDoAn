using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Auths;
using Backend.Application.DTOs.Users;
using Backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/auth")]
    [ApiController]
    public class AuthController : BaseController
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto obj)
        {
            var result = await _authService.LoginAsync(obj);

            return BaseResult(result);
        }

        [HttpPost("admin/login")]
        public async Task<IActionResult> AdminLogin([FromBody] LoginRequestDto obj)
        {
            var result = await _authService.AdminLoginAsync(obj);

            return BaseResult(result);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserSignUpDto obj)
        {
            var result = await _authService.RegisterAsync(obj);

            return BaseResult(result);
        }

        [AllowAnonymous]
        [HttpPost("admin/register")]
        public async Task<IActionResult> AdminRegisterAsync([FromBody] AdminRegisterDto obj)
        {
            var result = await _authService.AdminRegisterAsync(obj);
            return BaseResult(result);
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto obj)
        {
            obj.AccessToken = string.IsNullOrEmpty(obj.AccessToken) ? HttpContext.Request.Headers["Authorization"]
                .FirstOrDefault()?.Split(" ").Last() ?? string.Empty : obj.AccessToken;
            var result = await _authService.RefreshTokenAsync(obj);

            return BaseResult(result);
        }


        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequestDto obj)
        {
            var userId = this.GetLoggedInUserId();
            var result = await _authService.LogoutAsync(obj, userId);

            return BaseResult(result);
        }

        [Authorize]
        [HttpPost("logout-all-device")]
        public async Task<IActionResult> LogoutAllDevice()
        {
            var useId = this.GetLoggedInUserId();
            var result = await _authService.LogoutAllDeviceAsync(useId);

            return BaseResult(result);
        }


        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPasswordAsync([FromBody] ForgotPasswordDto dto)
        {
            var result = await _authService.ForgotPasswordAsync(dto.Email, dto.IsClientRequest);

            return BaseResult(result);
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var result = await _authService.ResetPasswordAsync(dto);
            return BaseResult(result);
        }

        [HttpPost("verify-code")]
        public async Task<IActionResult> VerifyCodeAsync([FromBody] VerifyCodeDto dto)
        {
            var result = await _authService.VerifyCodeAsync(dto);
            return BaseResult(result);
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Profile()
        {
            var userId = this.GetLoggedInUserId();
            var result = await _authService.GetProfileAsync(userId);

            return BaseResult(result);
        }

        [HttpPost("admin/create-end-user")]
        public async Task<IActionResult> CreateEndUser([FromBody] CreateEndUserDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _authService.AdminCreateEndUser(obj);
            return BaseResult(result);
        }

        [HttpGet("me/decentralization")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var result = await _authService.GetCurrentUserDecentralization();
            return BaseResult(result);
        }

        [HttpPost("resend-activation-mail")]
        public async Task<IActionResult> ResendActivationMail([FromBody] ResendActivationMailDto dto)
        {
            var result = await _authService.ResendActivationMailAsync(dto);
            return BaseResult(result);
        }

        [AllowAnonymous]
        [HttpGet("activate")]
        public IActionResult ActivateRedirect(
            [FromQuery] string code,
            [FromQuery] string email,
            [FromQuery] string purpose = "ACCOUNT_ACTIVATION")
        {
            // Build deep link — đây là scheme app Flutter đã đăng ký
            var deepLink = $"bookcarapp://verify?code={Uri.EscapeDataString(code)}&email={Uri.EscapeDataString(email)}&purpose={Uri.EscapeDataString(purpose)}";
            return Redirect(deepLink);
        }
    }
}
