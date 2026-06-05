using System;

namespace Backend.Domain.Abstractions.Entities;

    public interface IUserTracking
    {
        public int? CreatedBy { get; set; }
        public int? UpdatedBy { get; set; }
    }

