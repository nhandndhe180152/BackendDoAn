using System;

namespace Backend.Domain.Abstractions.Entities;

public interface IDateTracking
{
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}
