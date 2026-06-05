using System;

namespace Backend.Application.DTOs.Permissions;

public class UpdatePermissionDto : CreatePermissionDto
{
    public int? UpdatedBy { get; set; }
}
