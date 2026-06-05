using System;
using System.Text.Json.Serialization;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class ActionInMenu : EntityAuditBase<int>
    {
        public int ActionId { get; set; }
        public int MenuId { get; set; }
        [JsonIgnore]
        public virtual Action Action { get; set; }
        [JsonIgnore]
        public virtual Menu Menu { get; set; }
    }
