using System;
using Backend.Application.DTOs.UserStatuses;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IUserStatusService : IServiceBase<int, CreateUserStatusDto, UpdateUserStatusDto, DTParameter>
{
}
