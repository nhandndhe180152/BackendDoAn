using System;

namespace Backend.Domain.Aggregates;

public class UserVerificationTokenAggregate
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Purpose { get; set; } = null!;
    public bool IsUsed { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = null!;
}
