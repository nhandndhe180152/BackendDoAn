using System;
using JsonIgnoreAttribute = Newtonsoft.Json.JsonIgnoreAttribute;
using Backend.Domain.Abstractions;
using Backend.Share.Attributes;

namespace Backend.Domain.Entities;

public class User : EntityAuditBase<int>
{
    public string Username { get; set; } = null!;
    [SensitiveData]
    public string PasswordHash { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    [SensitiveData]
    public string Email { get; set; } = null!;
    [SensitiveData]
    public int? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? PhoneNumber { get; set; }
    public string? IdentityNumber { get; set; }
    public DateTime? DateOfIssue { get; set; }
    public string? PlaceOfIssue { get; set; }
    public string? MicrosoftId { get; set; }
    public int AccessFailedCount { get; set; }
    public bool LockEnabled { get; set; }
    public DateTime? LockEndDate { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public int UserStatusId { get; set; }
    public string? AddresDetail { get; set; }
    public int? AvatarId { get; set; }
    public virtual UserStatus UserStatus { get; set; } = null!;
    [JsonIgnore]
    public virtual FileUpload? Avatar { get; set; }
    [JsonIgnore]
    public virtual ICollection<UserRole> UserRoles { get; set; }
    [JsonIgnore]
    public virtual ICollection<UserNotification> UserNotifications { get; set; }
    [JsonIgnore]
    public virtual ICollection<UserVerificationToken> UserVerificationTokens { get; set; }
    [JsonIgnore]
    public virtual ICollection<UserDevice> UserDevices { get; set; }
    [JsonIgnore]
    public virtual ICollection<UserSession> UserSessions { get; set; }
}
