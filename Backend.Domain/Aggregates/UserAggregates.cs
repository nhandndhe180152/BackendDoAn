using System;
using Backend.Domain.Entities;

namespace Backend.Domain.Aggregates;

public class UserAggregates
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public bool LockEnabled { get; set; }
        public DateTime CreatedDate { get; set; }
        public int UserStatusId { get; set; }
        public string UserStatusName { get; set; } = null!;
        public string UserStatusColor { get; set; } = null!;
        public int? AvatarId { get; set; }
        public string? AvatarUrl { get; set; }
        public virtual List<Role> Roles { get; set; } = new List<Role>();
    }
