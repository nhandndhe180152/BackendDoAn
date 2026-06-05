using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Backend.Application.DTOs.Users;
using Backend.Application.Interfaces;
using Backend.Infrastructure.DependencyInjection.Options;
using Backend.Share.Constants;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Infrastructure.Services;

public class TokenProviderService : ITokenProviderService
{
    private readonly JwtSettings _jwtSettings;
    public TokenProviderService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public (string, DateTime) GenerateRefreshToken()
    {
        return (Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), DateTime.Now.AddMonths(_jwtSettings.RefreshTokenTtl));
    }

    public string GenerateToken(UserToken user)
    {
        var claims = new[]
        {
                new Claim(ClaimNames.ID, user.Id.ToString()),
                new Claim(ClaimNames.EMAIL, user.Email),
                new Claim(ClaimNames.JTI,user.AccessTokenJti),
                new Claim(ClaimNames.OFFICE_ID,(user.OfficeId ?? 0).ToString().ToString()),
                new Claim(ClaimNames.ROLE_IDS,string.Join(',',user.RoleIds)),
                new Claim(ClaimNames.DRIVER_ID, (user.DriverId??0).ToString())
            };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.Now.AddDays(_jwtSettings.ExpireTime),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public JwtSecurityToken ParseToken(string tokenString)
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);
        return token;
    }

    public bool ValidateToken(string authToken, bool requireExpirationTime)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = GetValidationParameters(requireExpirationTime);

        SecurityToken validatedToken;
        try
        {
            IPrincipal principal = tokenHandler.ValidateToken(authToken, validationParameters, out validatedToken);
            var jwtSecurityToken = validatedToken as JwtSecurityToken;
            if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                return false;
        }
        catch
        {
            return false;
        }
        return true;
    }

    private TokenValidationParameters GetValidationParameters(bool requireExpirationTime)
    {
        return new TokenValidationParameters()
        {
            ValidateLifetime = requireExpirationTime,
            ValidateAudience = true,
            ValidateIssuer = true,
            RequireExpirationTime = true,
            ValidIssuer = _jwtSettings.Issuer,
            ValidAudience = _jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey)), // The same key as the one that generate the token
            ClockSkew = TimeSpan.Zero
        };
    }
}
