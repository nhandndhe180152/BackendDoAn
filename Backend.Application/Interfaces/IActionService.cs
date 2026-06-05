using System;
using Backend.Application.DTOs.Actions;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IActionService : IServiceBase<int, CreateActionDto, UpdateActionDto, DTParameter>
{
}
