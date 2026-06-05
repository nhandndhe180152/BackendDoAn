using System;
using Backend.Share.Entities;

namespace Backend.Domain.DTParameters;

public class AuditLogDTParameters : DTParameter
{
    public int UserId { get; set; }
    public List<int> RoleIds { get; set; } = new List<int>();
    public List<string> TargetTypes { get; set; } = new List<string>();
    public List<string> Actions { get; set; } = new List<string>();
}
