using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Feedback : EntityAuditBase<int>
{
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
}
