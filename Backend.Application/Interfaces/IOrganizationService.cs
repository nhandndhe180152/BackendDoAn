using System;
using Backend.Application.DTOs.Organizations;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IOrganizationService : IServiceBase<int, CreateOrganizationDto, UpdateOrganizationDto, DTParameter>
{
}
