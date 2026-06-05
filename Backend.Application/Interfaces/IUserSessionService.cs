using System;
using Backend.Application.DTOs.UserSessions;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IUserSessionService : IServiceBase<int, CreateUserSessionDto, UpdateUserSessionDto, DTParameter>
{
}