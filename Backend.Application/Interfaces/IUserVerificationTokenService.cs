using System;
using Backend.Application.DTOs.UserVerificationTokens;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IUserVerificationTokenService : IServiceBase<int, CreateUserVerificationTokenDto, UpdateUserVerificationTokenDto, DTParameter>
{
}
