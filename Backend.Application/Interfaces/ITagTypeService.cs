using System;
using Backend.Application.DTOs.TagTypes;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface ITagTypeService : IServiceBase<int, CreateTagTypeDto, UpdateTagTypeDto, DTParameter>
{
}
