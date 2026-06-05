using System;

namespace Backend.Domain.Abstractions.Entities;

public interface IAuditable : IDateTracking, IUserTracking
{

}
