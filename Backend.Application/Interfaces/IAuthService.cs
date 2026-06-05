using System;
using Backend.Application.DTOs.Auths;
using Backend.Application.DTOs.Users;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IAuthService
{
    Task<ApiResponse> LoginAsync(LoginRequestDto obj);
    Task<ApiResponse> AdminLoginAsync(LoginRequestDto obj);
    Task<ApiResponse> RegisterAsync(UserSignUpDto obj);
    Task<ApiResponse> AdminRegisterAsync(AdminRegisterDto obj);
    Task<ApiResponse> RefreshTokenAsync(RefreshTokenRequestDto obj);
    Task<ApiResponse> LogoutAsync(LogoutRequestDto obj, int userId);
    Task<ApiResponse> LogoutAllDeviceAsync(int userId);
    Task<ApiResponse> ForgotPasswordAsync(string email, bool isClientRequest = false);
    Task<ApiResponse> VerifyCodeAsync(VerifyCodeDto dto);
    Task<ApiResponse> ResetPasswordAsync(ResetPasswordDto dto);
    Task<ApiResponse> GetProfileAsync(int userId);
    Task<ApiResponse> AdminCreateEndUser(CreateEndUserDto obj);
    Task<ApiResponse> GetCurrentUserDecentralization();
    Task<ApiResponse> ResendActivationMailAsync(ResendActivationMailDto dto);
}
