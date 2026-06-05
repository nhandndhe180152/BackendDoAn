using System;

namespace Backend.Application.DTOs.AuditLogs;

public class CreateAuditLogDto
{
    public string Action { get; set; } = null!;
    public string? Description { get; set; }
    public int? CreatedBy { get; set; }
}
