using System;

namespace Backend.Application.DTOs.Organizations;

public class UpdateOrganizationDto : CreateOrganizationDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
