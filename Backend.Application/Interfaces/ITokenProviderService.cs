using System;
using System.IdentityModel.Tokens.Jwt;
using Backend.Application.DTOs.Users;

namespace Backend.Application.Interfaces;

public interface ITokenProviderService
{
    string GenerateToken(UserToken user);
    (string, DateTime) GenerateRefreshToken();
    bool ValidateToken(string authToken, bool requireExpirationTime);
    JwtSecurityToken ParseToken(string tokenString);
}
